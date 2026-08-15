using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Notifications;

namespace TaskAdmin.Api.Realtime;

/// <summary>Entrega por SignalR a la app de escritorio (y a la web si está abierta).
/// Vive en el proyecto API porque necesita el hub; el resto del sistema solo ve
/// <see cref="INotificationChannel"/>.</summary>
public class DesktopChannel(
    IHubContext<AgentHub> hub,
    TaskAdminDbContext db,
    IOptionsMonitor<NotificationOptions> options) : INotificationChannel
{
    private NotificationOptions _options => options.CurrentValue;

    public NotificationChannel Kind => NotificationChannel.Desktop;

    /// <summary>Hay canal si algún dispositivo de la persona latió dentro de la tolerancia.
    /// Preguntarlo antes de intentar es lo que evita "entregar" a un cliente que no existe y
    /// dejar el check-in esperando un acuse que nunca va a llegar.</summary>
    public async Task<bool> CanDeliverAsync(User user, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.HeartbeatToleranceSeconds);

        return await db.DeviceRegistrations
            .AsNoTracking()
            .AnyAsync(d => d.UserId == user.Id
                        && d.RevokedAt == null
                        && d.LastHeartbeatAt != null
                        && d.LastHeartbeatAt >= cutoff, ct);
    }

    public async Task<DeliveryResult> SendAsync(
        User user,
        NotificationPayload payload,
        CancellationToken ct = default)
    {
        await hub.Clients.Group(AgentHub.GroupFor(user.Id)).SendAsync(
            "notification",
            new
            {
                kind = payload.Kind,
                title = payload.Title,
                body = payload.Body,
                linkPath = payload.LinkPath,
                checkInId = payload.CheckInId
            },
            ct);

        // Enviado no es entregado: quien confirma es la app, con AcknowledgeDelivery. Devolver
        // "ok" acá solo significa que el mensaje salió del servidor.
        return DeliveryResult.Ok();
    }
}
