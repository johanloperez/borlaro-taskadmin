using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure;

namespace Borlaro.Tms.Api.Endpoints;

public record DeviceDto(Guid Id, string? MachineName, string? AppVersion, DateTimeOffset? LastHeartbeatAt, bool IsAlive);

public record RegisterDeviceBody(string? MachineName, string? OsInfo);

public record CheckInDto(
    Guid Id,
    DateTimeOffset ScheduledAt,
    DateOnly LocalDate,
    CheckInStatus Status,
    NotificationChannel? DeliveredVia,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? OpenedAt,
    EscalationStep EscalationStep,
    int AttemptCount);

public record CoverageRow(Guid UserId, string Name, int Total, int Delivered, int Completed, int Missed);

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("/api/devices").WithTags("Dispositivos").RequireAuthorization();

        devices.MapPost("/", async (
            RegisterDeviceBody body,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.UserId()!.Value;

            var device = new DeviceRegistration
            {
                UserId = userId,
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
                MachineName = body.MachineName,
                OsInfo = body.OsInfo
            };

            db.DeviceRegistrations.Add(device);
            await db.SaveChangesAsync(ct);

            // El token se devuelve una sola vez, en el alta: después solo se ve el metadato.
            return Results.Ok(new { device.Id, device.Token });
        })
        .WithName("RegisterDevice");

        devices.MapGet("/", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            Microsoft.Extensions.Options.IOptions<Infrastructure.Notifications.NotificationOptions> options,
            CancellationToken ct) =>
        {
            var userId = principal.UserId()!.Value;
            var tolerance = TimeSpan.FromSeconds(options.Value.HeartbeatToleranceSeconds);
            var now = DateTimeOffset.UtcNow;

            var rows = await db.DeviceRegistrations
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.RevokedAt == null)
                .OrderByDescending(d => d.LastHeartbeatAt)
                .ToListAsync(ct);

            return Results.Ok(rows.Select(d => new DeviceDto(
                d.Id, d.MachineName, d.AppVersion, d.LastHeartbeatAt, d.IsAlive(now, tolerance))));
        })
        .WithName("ListDevices");

        devices.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.UserId()!.Value;
            var device = await db.DeviceRegistrations.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct);
            if (device is null) return Results.NotFound();

            device.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("RevokeDevice");

        var checkIns = app.MapGroup("/api/checkins").WithTags("Check-ins").RequireAuthorization();

        checkIns.MapGet("/mine", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.UserId()!.Value;

            var rows = await db.CheckIns
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LocalDate)
                .Take(30)
                .Select(c => new CheckInDto(
                    c.Id, c.ScheduledAt, c.LocalDate, c.Status, c.DeliveredVia,
                    c.DeliveredAt, c.OpenedAt, c.EscalationStep, c.Attempts.Count))
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("MyCheckIns");

        checkIns.MapPost("/{id:guid}/open", async (
            Guid id,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            // Mismo efecto que el acuse por SignalR, pero por HTTP: es la vía que usa el enlace
            // del email, donde no hay hub abierto.
            var userId = principal.UserId()!.Value;
            var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
            if (checkIn is null) return Results.NotFound();

            if (checkIn.OpenedAt is null)
            {
                var now = DateTimeOffset.UtcNow;
                checkIn.OpenedAt = now;
                checkIn.DeliveredAt ??= now;
                checkIn.Status = CheckInStatus.Opened;
                checkIn.NextEscalationAt = null;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new { checkIn.Id, checkIn.Status, checkIn.OpenedAt });
        })
        .WithName("OpenCheckIn");

        /// Panel de cobertura: la métrica que dice si el producto funciona. Si la tasa de
        /// finalización cae, no hay dashboard que valga.
        checkIns.MapGet("/coverage", async (BorlaroTmsDbContext db, CancellationToken ct) =>
        {
            var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

            var rows = await db.CheckIns
                .AsNoTracking()
                .Where(c => c.LocalDate >= since)
                .GroupBy(c => new { c.UserId, c.User!.Name })
                .Select(g => new CoverageRow(
                    g.Key.UserId,
                    g.Key.Name,
                    g.Count(),
                    g.Count(c => c.DeliveredAt != null),
                    g.Count(c => c.Status == CheckInStatus.Completed),
                    g.Count(c => c.Status == CheckInStatus.Missed)))
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("CheckInCoverage")
        .RequireAuthorization(Policies.CanManage);

        return app;
    }
}
