using System.Security.Claims;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Settings;

namespace Borlaro.Tms.Api.Endpoints;

/// <summary>Qué servidor consultar. Los dos vacíos = lo que la organización tenga guardado.</summary>
public record ProbeModelsBody(string? BaseUrl, string? ApiKey);

/// <summary>Qué modelo probar. Todo vacío = el que la organización tenga guardado.</summary>
public record ProbeModelBody(string? Provider, string? BaseUrl, string? Model, string? ApiKey);

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
            AgentModelHealth health,
            OrganizationSettings organization,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            var status = factory.Status(await organization.AgentModelAsync(ct));

            // El último rechazo del proveedor se muestra acá y no solo en el log: es la
            // diferencia entre «está configurado Groq» y «Groq devuelve 400 porque este modelo no
            // acepta herramientas», que es lo único que permite arreglarlo.
            var falla = health.Ultima(principal.OrganizationId());

            return Results.Ok(falla is null
                ? status
                : status with { LastError = falla.Message, LastErrorAt = falla.At });
        })
        .WithName("AgentModelStatus");

        /// <summary>Prueba el modelo de verdad, con una herramienta declarada.
        ///
        /// Es lo que distingue «el modelo responde» de «el modelo sirve para esto». Un modelo sin
        /// soporte de herramientas guarda bien, se ve bien, y falla recién en el primer check-in
        /// del día — que es exactamente lo que pasó con `allam-2-7b`.</summary>
        settings.MapPost("/model/probe", async (
            ProbeModelBody? body,
            AgentModelFactory factory,
            OrganizationSettings organization,
            CancellationToken ct) =>
        {
            var guardado = await organization.AgentModelAsync(ct);

            // Lo escrito en pantalla gana sobre lo guardado: se prueba antes de comprometer.
            var opciones = new AgentModelOptions
            {
                Provider = string.IsNullOrWhiteSpace(body?.Provider) ? guardado.Provider : body!.Provider,
                BaseUrl = string.IsNullOrWhiteSpace(body?.BaseUrl) ? guardado.BaseUrl : body!.BaseUrl,
                Model = string.IsNullOrWhiteSpace(body?.Model) ? guardado.Model : body!.Model,
                ApiKey = string.IsNullOrWhiteSpace(body?.ApiKey) ? guardado.ApiKey : body!.ApiKey,
                MaxTurns = 1,
                MaxTokens = 256
            };

            return Results.Ok(await factory.ProbeAsync(opciones, ct));
        })
        .WithName("ProbeModel");

        /// <summary>Los modelos que ofrece un servidor compatible con OpenAI —Ollama y LM Studio
        /// en la máquina, Groq o cualquier otro en la nube—. Se pregunta en vez de hacer escribir
        /// el nombre a mano: un typo no falla al guardar, falla en el primer check-in del día.
        ///
        /// **Es POST y no GET, y la clave va en el cuerpo.** Una clave de API en la query string
        /// termina en los registros del servidor, en el historial del navegador y en el `Referer`
        /// de lo que sea que se cargue después. Que sea un secreto de la propia organización no lo
        /// hace menos secreto.
        ///
        /// Los dos parámetros son opcionales: vacíos, se usa lo que la organización tenga
        /// guardado. Sirven para probar una URL y una clave **antes** de guardarlas, que es
        /// justamente cuando uno quiere saber si funcionan.</summary>
        settings.MapPost("/model/available", async (
            ProbeModelsBody? body,
            AgentModelFactory factory,
            OrganizationSettings organization,
            CancellationToken ct) =>
        {
            var guardado = await organization.AgentModelAsync(ct);

            var baseUrl = string.IsNullOrWhiteSpace(body?.BaseUrl) ? guardado.BaseUrl : body!.BaseUrl;
            var apiKey = string.IsNullOrWhiteSpace(body?.ApiKey) ? guardado.ApiKey : body!.ApiKey;

            return Results.Ok(await factory.AvailableModelsAsync(baseUrl, apiKey, ct));
        })
        .WithName("AvailableModels");

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
