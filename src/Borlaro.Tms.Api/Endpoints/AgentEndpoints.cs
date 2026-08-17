using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;

namespace Borlaro.Tms.Api.Endpoints;

public record ReplyBody(string Text);

public record RejectBody(string? Reason);

/// <summary>La superficie del agente: la conversación del check-in y la cola de aprobaciones.
///
/// El transcript es de la persona y de nadie más: todas las rutas de conversación filtran por el
/// usuario del token, así que un manager no puede leer la charla de su equipo por esta vía. Lo
/// que sí ve el manager son las acciones que salieron de ella, que es lo accionable.</summary>
public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var chat = app.MapGroup("/api/checkins").WithTags("Agente").RequireAuthorization();

        /// El check-in de hoy, o nada. Es lo que consulta la app de escritorio y la campana de
        /// la web para saber si hay algo que responder.
        chat.MapGet("/today", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.UserId()!.Value;

            var checkIn = await db.CheckIns
                .AsNoTracking()
                .Where(c => c.UserId == userId
                         && c.Status != CheckInStatus.Completed
                         && c.Status != CheckInStatus.Missed)
                .OrderByDescending(c => c.LocalDate)
                .Select(c => new { c.Id, c.LocalDate, c.Status, c.ScheduledAt })
                .FirstOrDefaultAsync(ct);

            return checkIn is null ? Results.NoContent() : Results.Ok(checkIn);
        })
        .WithName("TodayCheckIn");

        /// Abrir es lo que detiene la escalera y, la primera vez, lo que hace hablar al agente.
        chat.MapPost("/{id:guid}/conversation", async (
            Guid id,
            ClaimsPrincipal principal,
            CheckInConversation conversation,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await conversation.OpenAsync(id, principal.UserId()!.Value, ct))))
        .WithName("OpenConversation");

        chat.MapPost("/{id:guid}/messages", async (
            Guid id,
            ReplyBody body,
            ClaimsPrincipal principal,
            CheckInConversation conversation,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await conversation.ReplyAsync(id, principal.UserId()!.Value, body.Text, ct))))
        .WithName("ReplyToAgent");

        /// La salida rápida legítima. Sin ella, la persona cierra la ventana y el dato se pierde.
        chat.MapPost("/{id:guid}/no-changes", async (
            Guid id,
            ClaimsPrincipal principal,
            CheckInConversation conversation,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await conversation.CloseAsNoChangesAsync(id, principal.UserId()!.Value, ct))))
        .WithName("CloseCheckInNoChanges");

        chat.MapPost("/{id:guid}/complete", async (
            Guid id,
            ClaimsPrincipal principal,
            CheckInConversation conversation,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await conversation.CompleteAsync(id, principal.UserId()!.Value, ct))))
        .WithName("CompleteCheckIn");

        // ── Cola de aprobaciones ─────────────────────────────────────────────
        var actions = app.MapGroup("/api/agent-actions").WithTags("Agente")
            .RequireAuthorization(Policies.CanManage);

        actions.MapGet("/pending", async (AgentApprovalService approvals, CancellationToken ct) =>
            Results.Ok(await approvals.PendingAsync(ct)))
        .WithName("PendingAgentActions");

        actions.MapPost("/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal principal,
            AgentApprovalService approvals,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(new { message = await approvals.ApproveAsync(id, principal.UserId()!.Value, ct) })))
        .WithName("ApproveAgentAction");

        actions.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectBody body,
            ClaimsPrincipal principal,
            AgentApprovalService approvals,
            CancellationToken ct) =>
            await GuardAsync(async () =>
            {
                await approvals.RejectAsync(id, principal.UserId()!.Value, body.Reason, ct);
                return Results.NoContent();
            }))
        .WithName("RejectAgentAction");

        /// Lo que la IA tocó, en un solo lugar y reversible de un vistazo. Sin esta pantalla la
        /// promesa de «todo auditado» es una tabla en la base que nadie mira.
        actions.MapGet("/recent", async (BorlaroTmsDbContext db, CancellationToken ct) =>
        {
            var rows = await db.AgentActions
                .AsNoTracking()
                .Include(a => a.CheckIn!).ThenInclude(c => c.User)
                .Include(a => a.WorkItem!).ThenInclude(i => i.Project)
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync(ct);

            return Results.Ok(rows.Select(a => new
            {
                a.Id,
                a.CheckInId,
                Person = a.CheckIn?.User?.Name,
                a.ToolName,
                a.Status,
                a.IsError,
                a.Result,
                a.CreatedAt,
                WorkItem = a.WorkItem is null ? null : $"{a.WorkItem.Project!.Key}-{a.WorkItem.Number}"
            }));
        })
        .WithName("RecentAgentActions");

        return app;
    }

    /// <summary>Las reglas del dominio viajan como 400 con el mensaje tal cual: son cosas que la
    /// persona puede corregir («el check-in ya está cerrado»), no fallas del servidor.</summary>
    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
