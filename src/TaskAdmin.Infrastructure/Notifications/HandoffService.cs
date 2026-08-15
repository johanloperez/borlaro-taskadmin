using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;

namespace TaskAdmin.Infrastructure.Notifications;

/// <summary>Avisa cuando el trabajo cambia de manos.
///
/// Es una notificación distinta del check-in y por eso no reusa la escalera. Un check-in
/// pregunta «¿cómo viene lo tuyo?» y su falta de respuesta importa: por eso insiste, sube a email
/// y termina avisándole al responsable. Un relevo dice «te llegó esto», y si la persona no lo abre
/// en 45 minutos no hay nada que escalar — está trabajando en otra cosa, que es lo normal. Una
/// escalera acá entrenaría a todo el mundo a ignorar los avisos.
///
/// El handoff es, además, el hueco que la escalera no cubre: detecta que alguien no hizo su
/// check-in, no que una tarea llegó a una etapa y nadie se enteró. Eso último es invisible y es
/// donde se pierde el tiempo de verdad.</summary>
public class HandoffService(
    TaskAdminDbContext db,
    IEnumerable<INotificationChannel> channels,
    ILogger<HandoffService> logger)
{
    /// <summary>A quién le toca el trabajo que acaba de caer en esta etapa.
    ///
    /// Devuelve null si la etapa no define responsable —lo normal en etapas de tránsito como
    /// «Bloqueado», donde la tarea no cambia de manos— o si quien figuraba ya no está activo.</summary>
    public async Task<User?> ResolveOwnerAsync(WorkflowStage stage, CancellationToken ct = default)
    {
        if (stage.DefaultAssigneeId is not { } id) return null;

        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        // Un responsable inactivo es el modo de falla peligroso: si se le asignara igual, el
        // trabajo se apilaría en silencio sobre alguien que ya no entra al sistema, y nadie se
        // enteraría hasta que algo venciera. Se prefiere dejarlo sin asignar —que se ve— y
        // avisarle a quien puede arreglarlo.
        if (owner is null || !owner.IsActive)
        {
            logger.LogWarning(
                "La etapa {Etapa} tiene como responsable a alguien inactivo o inexistente", stage.Name);
            return null;
        }

        return owner;
    }

    /// <summary>Cuánto se espera antes de avisar, juntando lo que llegue en el medio.
    ///
    /// Dos minutos es el compromiso: suficiente para que un lote de tareas movidas de a una caiga
    /// en un solo aviso, y poco para que nadie sienta que el sistema tardó en avisarle. Un relevo
    /// no es urgente —la tarea está en su tablero igual— pero sí tiene que llegar el mismo rato.</summary>
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(2);

    /// <summary>Encola el relevo. No manda nada todavía: el barrido lo junta con los demás que le
    /// hayan caído a la misma persona y manda uno solo.</summary>
    public void Enqueue(
        Guid ownerId,
        Guid workItemId,
        string itemKey,
        string itemTitle,
        string stageName,
        string? fromPersonName)
    {
        db.PendingHandoffs.Add(new PendingHandoff
        {
            UserId = ownerId,
            WorkItemId = workItemId,
            ItemKey = itemKey,
            ItemTitle = itemTitle,
            StageName = stageName,
            FromName = fromPersonName
        });
    }

    /// <summary>Manda los avisos que ya cumplieron su ventana, uno por persona.</summary>
    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        var corte = DateTimeOffset.UtcNow - Ventana;

        var pendientes = await db.PendingHandoffs
            .Include(p => p.User)
            .Where(p => p.NotifiedAt == null && p.CreatedAt <= corte)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return 0;

        // Una sola marca para todo el barrido, no una por fila: las que se mandaron juntas tienen
        // que verse juntas. Con `UtcNow` adentro del bucle, tres avisos de un mismo mensaje
        // quedaban con tres marcas distintas por microsegundos, y después no había forma de leer
        // en la tabla si se había agrupado o no.
        var ahora = DateTimeOffset.UtcNow;

        foreach (var grupo in pendientes.GroupBy(p => p.UserId))
        {
            var owner = grupo.First().User;

            // Se marcan como avisados aunque no haya a quién avisarle: si no, la fila se queda
            // para siempre y el barrido la reintenta en cada vuelta.
            foreach (var p in grupo) p.NotifiedAt = ahora;

            if (owner is null || !owner.IsActive) continue;

            await EnviarAsync(owner, [.. grupo], ct);
        }

        await db.SaveChangesAsync(ct);
        return pendientes.Count;
    }

    /// <summary>Un aviso para un lote. Mejor-esfuerzo: que no haya canal vivo no puede deshacer
    /// movimientos de tareas que ya pasaron.</summary>
    private async Task EnviarAsync(User owner, IReadOnlyList<PendingHandoff> lote, CancellationToken ct)
    {
        var payload = lote.Count == 1 ? Uno(lote[0]) : Varios(lote);

        // Escritorio primero y email como reserva. Sin peldaños ni reintentos: es un aviso, y si
        // no llegó, la tarea igual está en su tablero cuando lo mire.
        foreach (var kind in new[] { NotificationChannel.Desktop, NotificationChannel.Email })
        {
            var channel = channels.FirstOrDefault(c => c.Kind == kind);
            if (channel is null) continue;

            try
            {
                if (!await channel.CanDeliverAsync(owner, ct)) continue;
                if ((await channel.SendAsync(owner, payload, ct)).Delivered) return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falló el aviso de relevo por {Canal}", kind);
            }
        }
    }

    private static NotificationPayload Uno(PendingHandoff p) => new(
        Kind: "handoff",
        Title: $"{p.ItemKey} es tuya",
        Body: p.FromName is null
            ? $"«{p.ItemTitle}» pasó a {p.StageName} y queda de tu lado."
            : $"{p.FromName} terminó su parte de «{p.ItemTitle}». Pasó a {p.StageName} y queda de tu lado.",
        // Al tablero y no al chat: lo que la persona necesita es ver la tarea, no conversar.
        LinkPath: "/proyectos",
        CheckInId: null);

    private static NotificationPayload Varios(IReadOnlyList<PendingHandoff> lote)
    {
        // Se nombran las tres primeras y se cuenta el resto. Un globo del sistema no da para
        // listar diez, y tres alcanzan para saber de qué se trata sin abrir nada.
        var nombradas = lote.Take(3).Select(p => p.ItemKey);
        var resto = lote.Count - 3;
        var cuales = resto > 0
            ? $"{string.Join(", ", nombradas)} y {resto} más"
            : string.Join(", ", nombradas);

        // Si todas cayeron en la misma etapa se dice cuál; si no, se omite antes que mentir.
        var etapas = lote.Select(p => p.StageName).Distinct().ToList();
        var donde = etapas.Count == 1 ? $" en {etapas[0]}" : "";

        return new NotificationPayload(
            Kind: "handoff",
            Title: $"Te llegaron {lote.Count} tareas",
            Body: $"Pasaron a tu lado{donde}: {cuales}.",
            LinkPath: "/proyectos",
            CheckInId: null);
    }

    /// <summary>Le cuenta a quien administra que una etapa quedó sin responsable válido. Sin esto,
    /// el ruteo automático falla callado, que es peor que no tenerlo.</summary>
    public async Task WarnOrphanStageAsync(
        string itemKey,
        string stageName,
        CancellationToken ct = default)
    {
        var admins = await db.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Manager || u.Role == UserRole.Admin))
            .ToListAsync(ct);

        var payload = new NotificationPayload(
            Kind: "handoff-orphan",
            Title: "Una etapa quedó sin responsable",
            Body: $"{itemKey} llegó a {stageName}, que tiene como responsable a alguien que ya no " +
                  "está activo. La tarea quedó sin asignar.",
            LinkPath: "/proyectos",
            CheckInId: null);

        var channel = channels.FirstOrDefault(c => c.Kind == NotificationChannel.Desktop);
        if (channel is null) return;

        foreach (var admin in admins)
        {
            try
            {
                if (await channel.CanDeliverAsync(admin, ct))
                {
                    await channel.SendAsync(admin, payload, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falló el aviso de etapa huérfana");
            }
        }
    }
}
