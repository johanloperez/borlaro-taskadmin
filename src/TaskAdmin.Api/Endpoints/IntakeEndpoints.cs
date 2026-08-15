using System.Security.Claims;
using System.Text.Json.Nodes;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Infrastructure.Services;

namespace TaskAdmin.Api.Endpoints;

public record EnableIntakeBody(string? Instructions);

public record IntakeFormDto(
    string ProjectName,
    string ItemNounSingular,
    string? Instructions,
    IReadOnlyList<string> WorkItemTypes,
    IReadOnlyList<CustomFieldDto> Fields);

public record IntakeSubmissionBody(
    string Title,
    string? Description,
    string? Type,
    string SubmitterName,
    string SubmitterEmail,
    JsonObject? CustomFields);

public static class IntakeEndpoints
{
    /// <summary>Nombre de la política de límite de tasa aplicada a las rutas públicas.</summary>
    public const string RateLimitPolicy = "intake";

    /// <summary>Devuelve la respuesta de rechazo, o null si puede seguir.</summary>
    private static async Task<IResult?> DenyUnlessLeadAsync(
        ClaimsPrincipal principal,
        ProjectAccess access,
        string key,
        CancellationToken ct)
    {
        var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);

        if (!permissions.CanView) return Results.NotFound();

        return permissions.CanManageProject
            ? null
            : Results.Problem(
                "Solo el líder del proyecto o un administrador pueden abrir o cerrar el formulario público.",
                statusCode: StatusCodes.Status403Forbidden);
    }

    public static IEndpointRouteBuilder MapIntakeEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Administración (líder del proyecto o admin) ──────────────────────────
        // Abrir una puerta anónima de escritura sobre un tablero es del que responde por ese
        // tablero, no de cualquiera con rol de manager en la instancia.
        var admin = app.MapGroup("/api/projects/{key}/intake").WithTags("Intake").RequireAuthorization();

        admin.MapPost("/enable", async (
            string key,
            EnableIntakeBody body,
            ClaimsPrincipal principal,
            ProjectAccess access,
            IntakeService service,
            CancellationToken ct) =>
        {
            var denied = await DenyUnlessLeadAsync(principal, access, key, ct);
            if (denied is not null) return denied;

            try
            {
                var (token, _) = await service.EnableAsync(key, body.Instructions, ct);
                return Results.Ok(new { token, url = $"/intake/{token}" });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("EnableIntake");

        admin.MapPost("/disable", async (
            string key,
            ClaimsPrincipal principal,
            ProjectAccess access,
            IntakeService service,
            CancellationToken ct) =>
        {
            var denied = await DenyUnlessLeadAsync(principal, access, key, ct);
            if (denied is not null) return denied;

            try
            {
                await service.DisableAsync(key, ct);
                return Results.NoContent();
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("DisableIntake");

        // ── Público (anónimo, con límite de tasa) ────────────────────────────────
        var pub = app.MapGroup("/api/intake").WithTags("Intake")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        pub.MapGet("/{token}", async (string token, IntakeService service, CancellationToken ct) =>
        {
            var project = await service.FindByTokenAsync(token, ct);

            // 404 y no 403: un token inválido y uno deshabilitado tienen que ser
            // indistinguibles desde afuera, o el endpoint sirve para sondear cuáles existen.
            if (project is null) return Results.NotFound();

            return Results.Ok(new IntakeFormDto(
                project.Name,
                project.ItemNounSingular,
                project.IntakeInstructions,
                project.WorkItemTypes,
                project.CustomFields
                    .OrderBy(f => f.Order)
                    .Select(f => new CustomFieldDto(
                        f.Id, f.Key, f.Label, f.Type, f.Options, f.Required, f.Order, null))
                    .ToList()));
        })
        .WithName("GetIntakeForm");

        pub.MapPost("/{token}", async (
            string token,
            IntakeSubmissionBody body,
            IntakeService service,
            CancellationToken ct) =>
        {
            try
            {
                var item = await service.SubmitAsync(
                    token,
                    new IntakeSubmission(
                        body.Title, body.Description, body.Type,
                        body.SubmitterName, body.SubmitterEmail, body.CustomFields),
                    ct);

                // Se devuelve solo el número: nada del estado interno del proyecto ni del
                // resto de la cola tiene por qué salir por una ruta anónima.
                return Results.Ok(new { received = true, reference = item.Number });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("SubmitIntake");

        return app;
    }
}
