using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;

namespace Borlaro.Tms.Api.Realtime;

/// <summary>Canal en vivo con la app de escritorio y con la web. Dos responsabilidades:
/// empujar notificaciones, y recibir los acuses que hacen avanzar (o detener) la escalera
/// de entrega.</summary>
[Authorize]
public class AgentHub(
    BorlaroTmsDbContext db,
    ProjectAccess access,
    ILogger<AgentHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.UserId();
        if (userId is not null)
        {
            // Grupo por usuario: una persona puede tener la web y la app de escritorio abiertas
            // a la vez, y la notificación tiene que llegar a las dos.
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Latido de la app de escritorio. Es lo que le dice al servidor que el canal
    /// Desktop está vivo; sin esto, la escalera salta directo a email.</summary>
    public async Task Heartbeat(string deviceToken, string? appVersion)
    {
        var userId = Context.User?.UserId();
        if (userId is null) return;

        var device = await db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.Token == deviceToken && d.UserId == userId && d.RevokedAt == null);

        if (device is null)
        {
            logger.LogWarning("Heartbeat con token de dispositivo desconocido o revocado");
            return;
        }

        device.LastHeartbeatAt = DateTimeOffset.UtcNow;
        if (appVersion is not null) device.AppVersion = appVersion;

        await db.SaveChangesAsync();
    }

    /// <summary>La app confirma que mostró el toast. No es lo mismo que haberlo visto.</summary>
    public async Task AcknowledgeDelivery(Guid checkInId)
    {
        var checkIn = await FindOwnCheckInAsync(checkInId);
        if (checkIn is null || checkIn.DeliveredAt is not null) return;

        checkIn.DeliveredAt = DateTimeOffset.UtcNow;
        checkIn.DeliveredVia = NotificationChannel.Desktop;
        if (checkIn.Status == CheckInStatus.Pending) checkIn.Status = CheckInStatus.Delivered;

        await db.SaveChangesAsync();
    }

    /// <summary>La persona abrió el chat. Este es el acuse que detiene la escalera: mientras
    /// falte, los peldaños siguen avanzando aunque el toast se haya mostrado.</summary>
    public async Task AcknowledgeOpened(Guid checkInId)
    {
        var checkIn = await FindOwnCheckInAsync(checkInId);
        if (checkIn is null || checkIn.OpenedAt is not null) return;

        var now = DateTimeOffset.UtcNow;
        checkIn.OpenedAt = now;
        checkIn.DeliveredAt ??= now;
        checkIn.Status = CheckInStatus.Opened;
        checkIn.NextEscalationAt = null;

        await db.SaveChangesAsync();
    }

    private async Task<Domain.Entities.CheckIn?> FindOwnCheckInAsync(Guid checkInId)
    {
        var userId = Context.User?.UserId();
        if (userId is null) return null;

        // Se filtra por usuario: un cliente no puede acusar recibo del check-in de otra persona
        // y apagarle la escalera.
        return await db.CheckIns.FirstOrDefaultAsync(c => c.Id == checkInId && c.UserId == userId);
    }

    public static string GroupFor(Guid userId) => $"user:{userId}";

    /// <summary>El grupo de un tablero. Por proyecto y no por organización a propósito: dentro de
    /// una empresa no todo el mundo ve todos los proyectos, y difundir a la organización entera
    /// filtraría que existe un proyecto —y qué se mueve en él— a quien no tiene acceso.</summary>
    public static string BoardGroup(string projectKey) => $"board:{projectKey.ToUpperInvariant()}";

    /// <summary>La pantalla dice qué tablero está mirando. Se comprueba el permiso acá y no se
    /// confía en el cliente: sin esto, cualquiera con sesión podría pedir el grupo de un proyecto
    /// ajeno y quedar escuchando sus movimientos.</summary>
    public async Task<bool> WatchBoard(string projectKey)
    {
        var userId = Context.User?.UserId();
        if (userId is null || string.IsNullOrWhiteSpace(projectKey)) return false;

        var permissions = await access.ForProjectKeyAsync(userId, projectKey);
        if (!permissions.CanView)
        {
            logger.LogWarning("Suscripción rechazada al tablero {Proyecto}", projectKey);
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(projectKey));
        return true;
    }

    /// <summary>La pantalla dejó de mirar ese tablero. No es crítico —al desconectarse el grupo se
    /// limpia solo— pero evita que una sesión larga navegando entre proyectos termine recibiendo
    /// todos los tableros que visitó.</summary>
    public async Task UnwatchBoard(string projectKey)
    {
        if (string.IsNullOrWhiteSpace(projectKey)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(projectKey));
    }
}
