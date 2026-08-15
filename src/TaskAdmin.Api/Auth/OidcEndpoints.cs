using System.ComponentModel.DataAnnotations;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;
using TaskAdmin.Infrastructure.Storage;

namespace TaskAdmin.Api.Auth;

/// <summary>Lo único que la pantalla de entrada necesita saber antes de que nadie se autentique:
/// si hay botón y qué dice. Ni la autoridad ni el ID de cliente hacen falta acá —el navegador
/// nunca arma la URL de autorización, la arma el servidor— así que no se publican.</summary>
public record OidcPublicInfo(bool Enabled, string Label);

public record ExchangeTicketBody([Required] string Ticket);

/// <summary>Qué resultó del ticket. Un solo tipo para los dos casos —sesión iniciada, o alta a
/// medio camino— para que el frontend no tenga que adivinar por el status code cuál de los dos
/// recibió.</summary>
public record TicketResult(bool Registration, string? Email, string? SuggestedName, AuthResponse? Session);

/// <summary>Entrar con el proveedor de identidad de la organización.
///
/// Cuatro rutas y ninguna de más: la que dice si el botón existe, la que arranca el flujo, la
/// vuelta del proveedor, y el canje del ticket por la sesión. Las tres primeras son anónimas por
/// definición —quien las usa todavía no tiene sesión— y la cuarta lo es porque el ticket *es* la
/// credencial: de un solo uso, válido dos minutos y emitido contra un intento concreto.</summary>
public static class OidcEndpoints
{
    public static IEndpointRouteBuilder MapOidcEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/oidc").WithTags("Auth").AllowAnonymous();

        group.MapGet("/", (OidcService oidc) =>
        {
            var o = oidc.Options;
            return Results.Ok(new OidcPublicInfo(o.IsUsable, o.ButtonLabel));
        })
        .WithName("OidcInfo");

        /// `volver` es a dónde ir después de entrar, para que un enlace a un check-in que llegó
        /// por email no termine en la pantalla de proyectos. Se valida como ruta relativa: una URL
        /// completa convertiría al login en un redirector abierto.
        group.MapGet("/start", async (string? volver, OidcService oidc, CancellationToken ct) =>
        {
            var url = await oidc.BuildAuthorizeUrlAsync(volver, ct);

            return url is null
                ? Results.Redirect(oidc.FailureRedirect(OidcFailure.Config, null))
                : Results.Redirect(url);
        })
        .WithName("OidcStart");

        group.MapGet("/callback", async (
            string? code,
            string? state,
            string? error,
            OidcService oidc,
            CancellationToken ct) =>
        {
            // Cancelar en la pantalla del proveedor no es un error que haya que explicarle a
            // nadie: vuelve a la pantalla de entrada y listo.
            if (error == "access_denied") return Results.Redirect(oidc.LoginRedirect());
            if (!string.IsNullOrEmpty(error)) return Results.Redirect(oidc.FailureRedirect(OidcFailure.Provider, null));

            var result = await oidc.HandleCallbackAsync(code, state, ct);

            return result.Failure is OidcFailure.None && result.Ticket is not null
                ? Results.Redirect(oidc.TicketRedirect(result.Ticket, result.ReturnPath))
                : Results.Redirect(oidc.FailureRedirect(result.Failure, result.Email));
        })
        .WithName("OidcCallback");

        /// El ticket se canjea por POST y no por GET: así el token de sesión no aparece nunca en
        /// una URL, ni en el historial del navegador, ni en el log de acceso de un proxy.
        group.MapPost("/exchange", async (
            ExchangeTicketBody body,
            OidcService oidc,
            TokenService tokens,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            // Un ticket puede ser dos cosas: la sesión de alguien que ya tiene cuenta, o el
            // permiso para crear una organización. Se mira antes de canjear, porque el canje gasta
            // el único uso que tiene y del lado del registro todavía falta un paso.
            var attempt = await oidc.PeekTicketAsync(body.Ticket, ct);

            if (attempt is { } pending && pending.IsRegistration)
            {
                return Results.Ok(new TicketResult(
                    Registration: true,
                    Email: pending.PendingEmail,
                    SuggestedName: pending.PendingName,
                    Session: null));
            }

            var user = await oidc.RedeemTicketAsync(body.Ticket, ct);
            if (user is null)
            {
                return Results.Problem("El ticket de entrada no es válido o ya venció. Volvé a entrar.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var (token, expiresAt) = tokens.Issue(user);
            return Results.Ok(new TicketResult(
                Registration: false,
                Email: user.Email,
                SuggestedName: null,
                Session: new AuthResponse(token, expiresAt, await AuthEndpoints.ToResponseAsync(db, user, ct))));
        })
        .WithName("OidcExchange");

        /// El segundo paso del alta: ya sabemos quién es —lo verificó el proveedor— y falta que
        /// diga cómo se llama su organización. Devuelve la sesión ya iniciada: pedirle que entre de
        /// nuevo justo después de registrarse sería un paso sin ninguna función.
        group.MapPost("/registro", async (
            HttpRequest request,
            OidcService oidc,
            OrganizationService organizations,
            TokenService tokens,
            TaskAdminDbContext db,
            IFileStore files,
            CancellationToken ct) =>
        {
            // Multipart: el logo viaja en el mismo pedido que el alta.
            if (!request.HasFormContentType)
            {
                return Results.Problem("Se espera multipart/form-data.", statusCode: 400);
            }

            var form = await request.ReadFormAsync(ct);

            var ticket = form["ticket"].FirstOrDefault();
            var organizationName = form["organizationName"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(organizationName))
            {
                return Results.Problem("Faltan el ticket o el nombre de la organización.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string? logoPath = null;
            var logo = form.Files.GetFile("logo");
            if (logo is { Length: > 0 })
            {
                try
                {
                    logoPath = await LogoUpload.SaveAsync(files, logo, ct);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
                }
            }

            try
            {
                var (user, error) = await oidc.RegisterAsync(
                    ticket, organizationName, logoPath, organizations, ct);

                if (user is null)
                {
                    // El alta falló: el logo no quedó huérfano.
                    if (logoPath is not null) await files.DeleteAsync(logoPath, ct);
                    return Results.Problem(error ?? "No se pudo crear la organización.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var (token, expiresAt) = tokens.Issue(user);
                return Results.Ok(new AuthResponse(token, expiresAt, await AuthEndpoints.ToResponseAsync(db, user, ct)));
            }
            catch (Exception)
            {
                if (logoPath is not null) await files.DeleteAsync(logoPath, ct);
                throw;
            }
        })
        .WithName("OidcRegister");

        return app;
    }
}
