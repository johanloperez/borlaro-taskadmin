using System.Security.Claims;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Infrastructure.Services;

namespace TaskAdmin.Api.Endpoints;

public record SendMessageBody(string Body);

/// <summary>Mensajes directos. Se le puede escribir a quien tiene trabajo asignado en los
/// proyectos que uno lidera, y responderle a quien escribió primero. Nada más.
///
/// La regla sale de para qué existe el canal: escribirle a alguien es sobre el trabajo que le
/// diste. Sin eso, cualquier cuenta con rol de manager le escribía a toda la instancia.</summary>
public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var messages = app.MapGroup("/api/messages").WithTags("Mensajes").RequireAuthorization();

        messages.MapGet("/", async (
            ClaimsPrincipal principal,
            MessagingService service,
            CancellationToken ct) =>
            Results.Ok(await service.ThreadsAsync(principal.UserId()!.Value, ct)))
        .WithName("ListThreads");

        messages.MapGet("/unread", async (
            ClaimsPrincipal principal,
            MessagingService service,
            CancellationToken ct) =>
            Results.Ok(new { count = await service.UnreadCountAsync(principal.UserId()!.Value, ct) }))
        .WithName("UnreadMessages");

        messages.MapGet("/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            MessagingService service,
            CancellationToken ct) =>
            Results.Ok(await service.ThreadAsync(principal.UserId()!.Value, userId, ct)))
        .WithName("ReadThread");

        messages.MapPost("/{userId:guid}", async (
            Guid userId,
            SendMessageBody body,
            ClaimsPrincipal principal,
            MessagingService service,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.SendAsync(principal.UserId()!.Value, userId, body.Body, ct));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SendMessage");

        return app;
    }
}
