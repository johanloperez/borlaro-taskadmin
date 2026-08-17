using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;

namespace Borlaro.Tms.Infrastructure.Notifications;

/// <summary>Las novedades que quien lidera un proyecto tiene que saber sin ir a buscarlas.
///
/// Existía la tabla `Notifications` desde la escalera de entrega, pero no había dónde leerla
/// adentro de la aplicación: se usaba solo como registro de lo que se había empujado al
/// escritorio. Acá se convierte en un feed con destinatarios propios.
///
/// Es un feed y no una bandeja de entrada: no se contesta desde ahí, cada novedad enlaza al lugar
/// donde se resuelve. Mezclarlo con los mensajes directos habría hecho que lo automático tape lo
/// humano, que es el modo en que estas dos cosas siempre conviven mal.</summary>
public class FeedService(
    BorlaroTmsDbContext db,
    IEnumerable<INotificationChannel> channels,
    ILogger<FeedService> logger)
{
    public const string KindOverdue = "item.overdue";
    public const string KindCheckInOpened = "checkin.opened";
    public const string KindCheckInAnswered = "checkin.answered";
    public const string KindTimeExtended = "item.time_extended";
    public const string KindStageChangedByAgent = "item.stage_changed_by_agent";
    public const string KindStartedWithoutDate = "item.started_without_date";

    /// <summary>A quiénes les toca enterarse de lo que pasa en un proyecto: quienes lo lideran,
    /// más los administradores de la organización.
    ///
    /// No se avisa a todo el equipo a propósito. Una novedad que le llega a diez personas no la
    /// atiende ninguna —cada una supone que la mira otra— y además convierte el feed en ruido
    /// para quien solo ejecuta su parte.</summary>
    private async Task<List<User>> DestinatariosAsync(Guid? projectId, CancellationToken ct)
    {
        var admins = await db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .ToListAsync(ct);

        if (projectId is not { } id) return admins;

        var lideres = await db.ProjectMembers
            .Where(m => m.ProjectId == id && m.Role == ProjectRole.Lead)
            .Select(m => m.User!)
            .Where(u => u.IsActive)
            .ToListAsync(ct);

        // Un administrador que además lidera el proyecto recibiría dos avisos idénticos.
        return admins.Concat(lideres).DistinctBy(u => u.Id).ToList();
    }

    /// <summary>A quiénes les toca enterarse de algo que le pasó a una **persona** y no a un
    /// proyecto: un check-in no pertenece a ningún tablero en particular.
    ///
    /// Se resuelve por dónde tiene trabajo abierto: quien lidera un proyecto donde esa persona
    /// está ejecutando algo es exactamente quien necesita saber que respondió y qué dijo. Alguien
    /// sin trabajo abierto en ningún lado solo llega a los administradores, que es lo correcto —no
    /// hay ningún líder a quien le importe.</summary>
    private async Task<List<User>> DestinatariosDePersonaAsync(Guid userId, CancellationToken ct)
    {
        var proyectos = await db.WorkItems
            .Where(i => i.AssigneeId == userId && i.ClosedAt == null)
            .Select(i => i.ProjectId)
            .Distinct()
            .ToListAsync(ct);

        var admins = await db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .ToListAsync(ct);

        if (proyectos.Count == 0) return admins;

        var lideres = await db.ProjectMembers
            .Where(m => proyectos.Contains(m.ProjectId) && m.Role == ProjectRole.Lead)
            .Select(m => m.User!)
            .Where(u => u.IsActive)
            .ToListAsync(ct);

        return admins.Concat(lideres).DistinctBy(u => u.Id).ToList();
    }

    /// <summary>Lo mismo que `PublicarAsync` pero para lo que le pasa a una persona: los
    /// check-ins. Nunca le avisa a la persona de lo que ella misma acaba de hacer.</summary>
    public async Task PublicarDePersonaAsync(
        Guid userId,
        string kind,
        string title,
        string body,
        string? linkUrl,
        CancellationToken ct = default)
    {
        var destinatarios = (await DestinatariosDePersonaAsync(userId, ct))
            .Where(u => u.Id != userId)
            .ToList();

        await EmitirAsync(destinatarios, kind, title, body, linkUrl, ct);
    }

    /// <summary>Escribe la novedad para cada destinatario y la empuja al escritorio.
    ///
    /// El `excluir` es para no avisarle a alguien de algo que acaba de hacer: quien lidera y
    /// además respondió su propio check-in no necesita que le cuenten que respondió.
    ///
    /// No hace `SaveChanges`: lo hace quien llama, junto con el cambio que originó la novedad. Un
    /// aviso que se guarda por su cuenta puede sobrevivir a una transacción que después falla, y
    /// entonces cuenta algo que no pasó.</summary>
    public async Task PublicarAsync(
        Guid? projectId,
        string kind,
        string title,
        string body,
        string? linkUrl,
        Guid? excluir = null,
        CancellationToken ct = default)
    {
        var destinatarios = (await DestinatariosAsync(projectId, ct))
            .Where(u => u.Id != excluir)
            .ToList();

        await EmitirAsync(destinatarios, kind, title, body, linkUrl, ct);
    }

    /// <summary>Escribe las filas y las empuja. Es la mitad común de las dos formas de publicar:
    /// lo único que cambia entre ellas es cómo se arma la lista de destinatarios.</summary>
    private async Task EmitirAsync(
        List<User> destinatarios,
        string kind,
        string title,
        string body,
        string? linkUrl,
        CancellationToken ct)
    {
        if (destinatarios.Count == 0) return;

        foreach (var persona in destinatarios)
        {
            db.Notifications.Add(new Notification
            {
                UserId = persona.Id,
                Channel = NotificationChannel.InApp,
                Kind = kind,
                Title = title,
                Body = body,
                LinkUrl = linkUrl
            });
        }

        // Y además al escritorio, para lo que no puede esperar a que alguien entre a la web.
        // Nunca puede tumbar la operación: la novedad ya está escrita, y que el hub esté caído
        // significa que se va a leer más tarde, no que no pasó.
        var desktop = channels.FirstOrDefault(c => c.Kind == NotificationChannel.Desktop);
        if (desktop is null) return;

        var payload = new NotificationPayload(kind, title, body, linkUrl ?? "/novedades", null);

        foreach (var persona in destinatarios)
        {
            try
            {
                if (await desktop.CanDeliverAsync(persona, ct))
                {
                    await desktop.SendAsync(persona, payload, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falló el empuje de la novedad {Kind}", kind);
            }
        }
    }

    /// <summary>Avisa de las tareas que vencieron y todavía nadie cerró.
    ///
    /// Va en un barrido y no en un disparador porque vencer no es algo que alguien haga: es que
    /// pase el tiempo. Y cada tarea se avisa **una sola vez**, marcada en `OverdueNotifiedAt`; sin
    /// esa marca el barrido vuelve a contar lo mismo cada treinta segundos y el feed queda
    /// inservible en una tarde.</summary>
    public async Task<int> BarrerVencidasAsync(CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var vencidas = await db.WorkItems
            .Include(i => i.Project)
            .Include(i => i.Assignee)
            .Where(i => i.ClosedAt == null
                     && !i.Project!.IsArchived
                     && i.DueDate != null
                     && i.DueDate < hoy
                     && i.OverdueNotifiedAt == null)
            // Un tope por pasada: si una instalación arranca con mil tareas vencidas, avisarlas
            // todas de una haría mil filas por destinatario y un correo por cada una. Se van
            // drenando de a poco, y el orden por fecha hace que salgan primero las más viejas.
            .OrderBy(i => i.DueDate)
            .Take(50)
            .ToListAsync(ct);

        if (vencidas.Count == 0) return 0;

        foreach (var item in vencidas)
        {
            var dias = hoy.DayNumber - item.DueDate!.Value.DayNumber;
            var responsable = item.Assignee?.Name ?? "sin responsable";
            var key = $"{item.Project!.Key}-{item.Number}";

            await PublicarAsync(
                item.ProjectId,
                KindOverdue,
                $"{key} venció hace {dias} día(s)",
                $"«{item.Title}» — {responsable}. Vencía el {item.DueDate:dd/MM} y sigue abierta.",
                $"/p/{item.Project.Key}?item={item.Id}",
                excluir: null,
                ct);

            item.OverdueNotifiedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return vencidas.Count;
    }

    /// <summary>Cuánto vive una novedad ya leída. Un mes es lo que tarda alguien en volver de
    /// vacaciones y todavía querer entender qué pasó; más atrás que eso, lo que se busca es la
    /// actividad del proyecto, que sí es un registro completo y no se poda.</summary>
    public static readonly TimeSpan RetencionLeidas = TimeSpan.FromDays(30);

    /// <summary>Borra las novedades leídas que ya pasaron su retención.
    ///
    /// Hace falta porque cada novedad escribe **una fila por destinatario**: un proyecto con tres
    /// líderes y dos administradores multiplica por cinco cada aviso, y hasta acá lo único que
    /// vaciaba esta tabla era borrar la organización entera.
    ///
    /// Solo las leídas. Una no leída no se borra por vieja: que nadie la haya mirado en un mes es
    /// un problema, y desaparecerla lo esconde en vez de resolverlo.</summary>
    public async Task<int> PodarLeidasAsync(CancellationToken ct = default)
    {
        var corte = DateTimeOffset.UtcNow - RetencionLeidas;

        return await db.Notifications
            .Where(n => n.ReadAt != null && n.ReadAt < corte)
            .ExecuteDeleteAsync(ct);
    }
}
