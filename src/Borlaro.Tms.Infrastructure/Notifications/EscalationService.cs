using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Infrastructure.Notifications;

/// <summary>La escalera de entrega. Un check-in no es "mandar una notificación y olvidarse":
/// es una máquina de estados que sigue insistiendo por canales cada vez más molestos hasta que
/// la persona lo abre, y si nunca lo abre, se asegura de que el manager se entere.
///
/// El acuse que la detiene es OpenedAt, no DeliveredAt: que el toast se haya mostrado no
/// significa que alguien lo haya visto.</summary>
public class EscalationService(
    BorlaroTmsDbContext db,
    IEnumerable<INotificationChannel> channels,
    IOptionsMonitor<NotificationOptions> options,
    ILogger<EscalationService> logger)
{
    // Por monitor: los minutos de cada peldaño se editan desde la interfaz y el próximo barrido
    // ya tiene que usarlos.
    private NotificationOptions _options => options.CurrentValue;

    /// <summary>Avanza todos los check-ins cuyo próximo peldaño ya venció, en toda la
    /// instalación.
    ///
    /// El barrido es lo contrario de un request: no tiene una organización, las tiene a todas. La
    /// búsqueda va sin filtro y cada check-in se atiende dentro de la suya, que es lo que hace que
    /// el aviso al manager le llegue a los managers de su empresa y no a los de la de al lado.</summary>
    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        List<CheckIn> due;
        using (OrganizationScope.UseSystem())
        {
            due = await db.CheckIns
                .Include(c => c.User)
                .Where(c => c.NextEscalationAt != null
                         && c.NextEscalationAt <= now
                         && c.OpenedAt == null
                         && c.Status != CheckInStatus.Completed
                         && c.Status != CheckInStatus.Missed)
                .ToListAsync(ct);
        }

        foreach (var checkIn in due)
        {
            // Se guarda adentro del alcance y no al final del bucle: lo que se escribe acá
            // —intentos de entrega, avisos— se estampa al guardar, y afuera no habría con qué.
            using var scope = OrganizationScope.Use(checkIn.OrganizationId);

            await AdvanceAsync(checkIn, now, ct);
            await db.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    /// <summary>Dispara el primer peldaño de un check-in recién creado.</summary>
    public async Task StartAsync(CheckIn checkIn, CancellationToken ct = default)
    {
        using var scope = OrganizationScope.Use(checkIn.OrganizationId);

        checkIn.EscalationStep = EscalationStep.FirstDirectPing;
        await AdvanceAsync(checkIn, DateTimeOffset.UtcNow, ct, isFirst: true);
        await db.SaveChangesAsync(ct);
    }

    private async Task AdvanceAsync(
        CheckIn checkIn,
        DateTimeOffset now,
        CancellationToken ct,
        bool isFirst = false)
    {
        var step = isFirst ? EscalationStep.FirstDirectPing : NextStep(checkIn.EscalationStep);
        var user = checkIn.User ?? await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);

        if (step == EscalationStep.MarkedMissed)
        {
            checkIn.Status = CheckInStatus.Missed;
            checkIn.EscalationStep = step;
            checkIn.NextEscalationAt = null;

            logger.LogWarning("Check-in {CheckIn} de {User} marcado como perdido", checkIn.Id, user.Email);
            await NotifyManagersAsync(user, checkIn, ct);
            return;
        }

        var esPersonal = step is EscalationStep.FirstDirectPing or EscalationStep.SecondDirectPing;
        var payload = await BuildPayloadAsync(step, user, checkIn, ct);

        // El peldaño dice el rol; el canal concreto sale de la persona. En los dos primeros se
        // prueban sus canales personales en orden y se usa el primero que pueda entregar ahora
        // mismo; en los dos últimos, el correo, que es el único que no exige haber instalado ni
        // vinculado nada.
        var candidatos = esPersonal
            ? await PersonalChannelsAsync(user, ct)
            : channels.Where(c => c.Kind == NotificationChannel.Email).ToList();

        if (candidatos.Count == 0)
        {
            RecordAttempt(checkIn, user.PreferredChannel ?? NotificationChannel.Email, step, now,
                DeliveryResult.Failed("canal no registrado"));
        }
        else
        {
            INotificationChannel? elegido = null;

            foreach (var candidato in candidatos)
            {
                if (await candidato.CanDeliverAsync(user, ct))
                {
                    elegido = candidato;
                    break;
                }

                // Queda registrado por qué no se usó: «no le llegó» y «no había por dónde» son
                // dos historias distintas cuando alguien pregunta al día siguiente.
                RecordAttempt(checkIn, candidato.Kind, step, now,
                    DeliveryResult.Failed("canal no disponible"));
            }

            if (elegido is null)
            {
                // Ningún canal personal puede entregar —nadie con la app abierta, nadie con Slack
                // vinculado—. No tiene sentido gastar los 15 y 45 minutos de los dos primeros
                // peldaños esperando un acuse imposible: se salta directo al correo.
                if (esPersonal)
                {
                    checkIn.EscalationStep = EscalationStep.SecondDirectPing;
                    checkIn.NextEscalationAt = now;
                    return;
                }
            }
            else
            {
                var result = await elegido.SendAsync(user, payload, ct);
                RecordAttempt(checkIn, elegido.Kind, step, now, result);

                // Solo el escritorio tiene acuse de apertura de verdad. Los demás se cuentan como
                // entregados al mandarlos, y lo que sigue faltando es que la persona lo abra —que
                // es lo que detiene la escalera, no la entrega.
                if (result.Delivered && elegido.Kind != NotificationChannel.Desktop)
                {
                    checkIn.DeliveredAt ??= now;
                    checkIn.DeliveredVia ??= elegido.Kind;
                    if (checkIn.Status == CheckInStatus.Pending) checkIn.Status = CheckInStatus.Delivered;
                }
            }
        }

        checkIn.EscalationStep = step;
        checkIn.NextEscalationAt = ScheduleNext(checkIn, step);
    }

    /// <summary>Los canales personales de esta persona, en el orden en que hay que probarlos.
    ///
    /// «Personal» es todo menos el correo y menos `InApp`: el correo es el último recurso de la
    /// escalera y no compite acá, e `InApp` no le avisa a nadie —es la campana que se ve cuando ya
    /// entraste—. El preferido va primero si lo eligió; el resto queda en el orden del enum, que
    /// es estable.</summary>
    private async Task<List<INotificationChannel>> PersonalChannelsAsync(User user, CancellationToken ct)
    {
        await Task.CompletedTask;

        return channels
            .Where(c => c.Kind is not (NotificationChannel.Email or NotificationChannel.InApp))
            .OrderBy(c => c.Kind == user.PreferredChannel ? 0 : 1)
            .ThenBy(c => (int)c.Kind)
            .ToList();
    }

    private static EscalationStep NextStep(EscalationStep current) => current switch
    {
        EscalationStep.FirstDirectPing => EscalationStep.SecondDirectPing,
        EscalationStep.SecondDirectPing => EscalationStep.FirstEmail,
        EscalationStep.FirstEmail => EscalationStep.SecondEmailAndManagerFeed,
        _ => EscalationStep.MarkedMissed
    };

    /// <summary>Los tiempos se cuentan desde el disparo del check-in, no desde el peldaño
    /// anterior: si un peldaño se saltea, la escalera no se corre hacia adelante.</summary>
    private DateTimeOffset? ScheduleNext(CheckIn checkIn, EscalationStep completed)
    {
        var origin = checkIn.ScheduledAt;

        return completed switch
        {
            EscalationStep.FirstDirectPing => origin.AddMinutes(_options.SecondToastAfterMinutes),
            EscalationStep.SecondDirectPing => origin.AddMinutes(_options.FirstEmailAfterMinutes),
            EscalationStep.FirstEmail => origin.AddMinutes(_options.SecondEmailAfterMinutes),
            EscalationStep.SecondEmailAndManagerFeed => origin.AddMinutes(_options.MarkMissedAfterMinutes),
            _ => null
        };
    }

    private async Task<NotificationPayload> BuildPayloadAsync(
        EscalationStep step,
        User user,
        CheckIn checkIn,
        CancellationToken ct)
    {
        var insistent = step != EscalationStep.FirstDirectPing;

        return new NotificationPayload(
            Kind: "checkin",
            Title: insistent ? "Tu check-in sigue pendiente" : "Check-in del día",
            Body: insistent
                ? $"{user.Name}, quedó pendiente tu check-in. Son 30 segundos y deja el tablero al día."
                : $"Buen día {user.Name}. {await WhatIsWaitingAsync(user.Id, ct)}",
            LinkPath: $"/checkin/{checkIn.Id}",
            CheckInId: checkIn.Id);
    }

    /// <summary>De qué va a hablar el check-in, nombrando las tareas.
    ///
    /// «¿Repasamos cómo viene tu trabajo?» es un aviso que no dice nada: obliga a abrir la app
    /// para saber si vale la pena. Nombrar lo que está atrasado o trabado convierte el globo en
    /// información — y es la misma razón por la que el agente abre con datos y no con «¿cómo
    /// vas?».</summary>
    private async Task<string> WhatIsWaitingAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await db.WorkItems
            .AsNoTracking()
            .Where(i => i.AssigneeId == userId && i.ClosedAt == null)
            .Include(i => i.Project)
            .Include(i => i.Blockers.Where(b => b.ResolvedAt == null))
            .OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate)
            .Take(3)
            .ToListAsync(ct);

        if (items.Count == 0) return "No tenés trabajo abierto asignado; lo vemos en un minuto.";

        var mentions = items.Select(i =>
        {
            var id = $"{i.Project!.Key}-{i.Number}";

            if (i.Blockers.Count > 0) return $"{id} bloqueada";
            if (i.DueDate is null) return id;
            if (i.DueDate < today) return $"{id} vencida";
            if (i.DueDate == today) return $"{id} vence hoy";

            return $"{id} vence {i.DueDate:dd/MM}";
        });

        var total = await db.WorkItems
            .CountAsync(i => i.AssigneeId == userId && i.ClosedAt == null, ct);

        var extra = total > items.Count ? $" y {total - items.Count} más" : string.Empty;

        return $"Hablemos de {string.Join(", ", mentions)}{extra}.";
    }

    private void RecordAttempt(
        CheckIn checkIn,
        NotificationChannel channel,
        EscalationStep step,
        DateTimeOffset now,
        DeliveryResult result)
    {
        db.NotificationAttempts.Add(new NotificationAttempt
        {
            CheckInId = checkIn.Id,
            Channel = channel,
            Step = step,
            SentAt = now,
            DeliveredAt = result.Delivered ? now : null,
            Error = result.Error
        });
    }

    /// <summary>Cuando un check-in no se entrega, el manager tiene que enterarse. El sistema
    /// informa; no sanciona.</summary>
    private async Task NotifyManagersAsync(User user, CheckIn checkIn, CancellationToken ct)
    {
        var managers = await db.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Manager || u.Role == UserRole.Admin))
            .ToListAsync(ct);

        foreach (var manager in managers)
        {
            db.Notifications.Add(new Notification
            {
                UserId = manager.Id,
                Channel = NotificationChannel.InApp,
                Kind = "checkin_missed",
                Title = $"{user.Name} no completó su check-in",
                Body = $"El check-in del {checkIn.LocalDate:dd/MM} quedó sin responder.",
                LinkUrl = "/equipo"
            });
        }
    }
}
