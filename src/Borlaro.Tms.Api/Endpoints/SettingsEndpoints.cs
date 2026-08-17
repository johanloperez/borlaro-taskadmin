using System.Security.Claims;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Settings;

namespace Borlaro.Tms.Api.Endpoints;

public record SaveSettingsBody(Dictionary<string, string?> Values);

/// <summary>La configuración de la instancia, editable por el admin desde la interfaz.
///
/// Las claves y contraseñas entran pero no salen: la respuesta dice si están definidas, nunca
/// cuál es el valor. Un GET que devuelve la clave de la API convierte cualquier XSS o log de
/// proxy mal configurado en una filtración.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/settings").WithTags("Configuración")
            .RequireAuthorization(Policies.IsAdmin);

        settings.MapGet("/", async (SettingsService service, CancellationToken ct) =>
            Results.Ok(await service.ReadAllAsync(SettingScope.Organization, ct)))
        .WithName("ReadSettings");

        /// Qué modelo está corriendo de verdad **en esta organización**. La configuración dice la
        /// intención; esto dice el hecho, que no coinciden cuando falta la clave y el agente cae al
        /// modelo de prueba.
        settings.MapGet("/model", async (
            AgentModelFactory factory,
            OrganizationSettings organization,
            CancellationToken ct) =>
            Results.Ok(factory.Status(await organization.AgentModelAsync(ct))))
        .WithName("AgentModelStatus");

        /// Los modelos que tiene instalados el servidor local (Ollama, LM Studio). Se pregunta en
        /// vez de hacer escribir el nombre a mano.
        settings.MapGet("/model/available", async (
            AgentModelFactory factory,
            OrganizationSettings organization,
            CancellationToken ct) =>
            Results.Ok(await factory.AvailableModelsAsync(
                (await organization.AgentModelAsync(ct)).BaseUrl, ct)))
        .WithName("AvailableLocalModels");

        /// Si el proveedor de identidad responde, y con qué URL de redirección hay que darlo de
        /// alta del otro lado. Pegar mal esa URL es el error más común de esta integración, y el
        /// proveedor lo reporta como `redirect_uri_mismatch` sin decir cuál esperaba.
        settings.MapGet("/oidc", async (OidcService oidc, CancellationToken ct) =>
            Results.Ok(await oidc.ProbeAsync(ct)))
        .WithName("OidcStatus");

        settings.MapPut("/", async (
            SaveSettingsBody body,
            ClaimsPrincipal principal,
            SettingsService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.SaveAsync(body.Values ?? [], principal.UserId(), SettingScope.Organization, ct);
                return Results.Ok(await service.ReadAllAsync(SettingScope.Organization, ct));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SaveSettings");

        // ── Los de la instalación, para quien la opera ───────────────────────────
        // Mismo servicio y misma pantalla, otro alcance y otra autoridad. El login con Google, la
        // duración de las sesiones o la URL pública no son decisiones que cada empresa pueda tomar
        // por su cuenta: hay un solo servidor.
        var platform = app.MapGroup("/api/platform/settings").WithTags("Plataforma")
            .RequireAuthorization(Policies.IsPlatformOperator);

        platform.MapGet("/", async (SettingsService service, CancellationToken ct) =>
            Results.Ok(await service.ReadAllAsync(SettingScope.Platform, ct)))
        .WithName("ReadPlatformSettings");

        platform.MapPut("/", async (
            SaveSettingsBody body,
            ClaimsPrincipal principal,
            SettingsService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.SaveAsync(body.Values ?? [], principal.UserId(), SettingScope.Platform, ct);
                return Results.Ok(await service.ReadAllAsync(SettingScope.Platform, ct));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SavePlatformSettings");

        platform.MapGet("/oidc", async (OidcService oidc, CancellationToken ct) =>
            Results.Ok(await oidc.ProbeAsync(ct)))
        .WithName("PlatformOidcStatus");

        return app;
    }
}
