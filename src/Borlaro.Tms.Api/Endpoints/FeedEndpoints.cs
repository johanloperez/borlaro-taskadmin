using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Infrastructure;

namespace Borlaro.Tms.Api.Endpoints;

/// <summary>El feed de novedades: lo que quien lidera un proyecto tiene que saber sin ir a
/// buscarlo. Tareas vencidas, el agente escribiéndole a alguien, la respuesta que dio, las horas
/// que se agregaron y las etapas que el agente movió.
///
/// Cada quien lee **solo lo suyo**: las novedades se escriben con destinatario, y acá no hay
/// ninguna forma de pedir las de otra persona. No es una restricción de rol sino de identidad —
/// un administrador tampoco lee el feed ajeno, porque el suyo ya trae todo lo que le toca.</summary>
public static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var feed = app.MapGroup("/api/novedades").WithTags("Novedades").RequireAuthorization();

        feed.MapGet("/", async (
            bool? soloNoLeidas,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId()!.Value;

            var query = db.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == me && n.Channel == NotificationChannel.InApp);

            if (soloNoLeidas == true) query = query.Where(n => n.ReadAt == null);

            // Un tope y no paginación: el feed se mira para ponerse al día, no para auditar. Lo
            // que pasó hace tres semanas se busca en la actividad del proyecto, que sí es un
            // registro completo.
            var rows = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(200)
                .Select(n => new
                {
                    n.Id,
                    n.Kind,
                    n.Title,
                    n.Body,
                    n.LinkUrl,
                    n.CreatedAt,
                    n.ReadAt
                })
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("ListFeed");

        feed.MapGet("/no-leidas", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId()!.Value;

            var count = await db.Notifications
                .CountAsync(n => n.UserId == me
                              && n.Channel == NotificationChannel.InApp
                              && n.ReadAt == null, ct);

            return Results.Ok(new { count });
        })
        .WithName("UnreadFeed");

        feed.MapPost("/{id:guid}/leida", async (
            Guid id,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId()!.Value;

            // El filtro por UserId no es solo para encontrarla: es lo que impide marcar como
            // leída la novedad de otro. Sin él, un id adivinado alcanzaría.
            var updated = await db.Notifications
                .Where(n => n.Id == id && n.UserId == me && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);

            return updated == 0 ? Results.NotFound() : Results.NoContent();
        })
        .WithName("MarkFeedRead");

        feed.MapPost("/leidas", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId()!.Value;

            await db.Notifications
                .Where(n => n.UserId == me
                         && n.Channel == NotificationChannel.InApp
                         && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);

            return Results.NoContent();
        })
        .WithName("MarkAllFeedRead");

        return app;
    }
}
