using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Notifications;
using TaskAdmin.Infrastructure.Services;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Api.Auth;

/// <summary>Por qué falló el login externo. Se traduce a un código corto en la URL de vuelta y a
/// una frase en castellano en la pantalla de entrada: el navegador no puede recibir el detalle
/// crudo del proveedor sin convertirse en un canal para inyectar texto en la página.</summary>
public enum OidcFailure
{
    None,
    /// <summary>El proveedor no está configurado, o quedó a medias.</summary>
    Config,
    /// <summary>El `state` no existe, ya se usó o venció.</summary>
    State,
    /// <summary>El proveedor devolvió un error, o el canje del código falló.</summary>
    Provider,
    /// <summary>El id_token no trae email, o el proveedor lo marca como no verificado.</summary>
    Email,
    /// <summary>El email no está en los dominios permitidos.</summary>
    Domain,
    /// <summary>Nadie con ese email en TaskAdmin. No se crea: alguien tiene que darlo de alta.</summary>
    NoAccount,
    /// <summary>La persona existe pero está desactivada.</summary>
    Inactive,
    /// <summary>Esa persona ya tiene otra cuenta del mismo proveedor vinculada.</summary>
    AlreadyLinked,

    /// <summary>Esa cuenta del proveedor existe en más de una organización y todavía no hay forma
    /// de preguntar a cuál entrar.</summary>
    Ambiguous
}

public record OidcCallbackResult(OidcFailure Failure, string? Ticket, string ReturnPath, string? Email)
{
    public static OidcCallbackResult Failed(OidcFailure failure, string returnPath = "/", string? email = null) =>
        new(failure, null, returnPath, email);
}

public record OidcProbe(bool Enabled, bool Complete, string RedirectUri, string? Issuer, string? Error);

/// <summary>Los documentos de descubrimiento, cacheados y refrescados solos. Es singleton porque
/// <see cref="ConfigurationManager{T}"/> guarda las claves públicas del proveedor y sabe cuándo
/// volver a pedirlas: uno nuevo por request tiraría un GET a Google en cada login.</summary>
public class OidcDiscoveryCache(IHttpClientFactory factory)
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _byAuthority = new();

    public Task<OpenIdConnectConfiguration> GetAsync(string authority, CancellationToken ct)
    {
        // Con la autoridad como clave, cambiarla desde Configuración estrena caché sin reiniciar
        // nada; la vieja queda sin uso y no molesta.
        var manager = _byAuthority.GetOrAdd(authority, key => new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{key.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(factory.CreateClient(OidcService.HttpClientName)) { RequireHttps = false }));

        return manager.GetConfigurationAsync(ct);
    }
}

/// <summary>Entrar con las credenciales que la persona ya tiene, sin que TaskAdmin guarde otra
/// contraseña más.
///
/// El flujo es «authorization code» con PKCE y el canje del lado del servidor: el navegador nunca
/// ve el secreto de cliente ni el id_token del proveedor. Lo que recibe al volver es un ticket de
/// un solo uso que canjea por POST — el token de sesión no puede viajar en el query string, donde
/// queda en el historial y en los logs de cualquier proxy intermedio.
///
/// **No da de alta a nadie.** Si el email que devuelve el proveedor no corresponde a una persona
/// ya cargada en /personas, el login se rechaza. Es coherente con el resto del producto: los
/// cuatro roles se asignan a mano, y el alta automática convertiría a cualquiera con una cuenta
/// del dominio en usuario activo sin que nadie lo aprobara.</summary>
public class OidcService(
    IOptionsMonitor<OidcOptions> options,
    IOptionsMonitor<NotificationOptions> notifications,
    IOptionsMonitor<TenancyOptions> tenancy,
    OidcDiscoveryCache discovery,
    IHttpClientFactory httpFactory,
    TaskAdminDbContext db,
    ILogger<OidcService> logger)
{
    public const string HttpClientName = "oidc";
    public const string CallbackPath = "/api/auth/oidc/callback";

    public OidcOptions Options => options.CurrentValue;

    /// <summary>La URL que hay que registrar en la consola del proveedor. Se calcula a partir de
    /// la URL pública para que el admin la copie y pegue en vez de deducirla —pegarla mal es el
    /// error más común de esta integración, y el proveedor lo reporta como `redirect_uri_mismatch`
    /// sin decir cuál esperaba.</summary>
    public string RedirectUri => $"{notifications.CurrentValue.PublicBaseUrl.TrimEnd('/')}{CallbackPath}";

    private string PublicBase => notifications.CurrentValue.PublicBaseUrl.TrimEnd('/');

    /// <summary>Arranca el flujo: guarda el estado del intento y devuelve a dónde mandar el
    /// navegador.</summary>
    public async Task<string?> BuildAuthorizeUrlAsync(string? returnPath, CancellationToken ct)
    {
        var o = Options;
        if (!o.IsUsable) return null;

        OpenIdConnectConfiguration config;
        try
        {
            config = await discovery.GetAsync(o.Authority, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer el descubrimiento de {Authority}", o.Authority);
            return null;
        }

        // Los intentos que nadie completó no sirven para nada después de una hora. Barrerlos acá
        // evita un job más para una tabla que crece de a una fila por login.
        var stale = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        await db.OidcLoginAttempts.Where(a => a.CreatedAt < stale).ExecuteDeleteAsync(ct);

        var verifier = RandomToken(48);
        var attempt = new OidcLoginAttempt
        {
            State = RandomToken(24),
            CodeVerifier = verifier,
            Nonce = RandomToken(24),
            ReturnPath = SafeReturnPath(returnPath)
        };

        db.OidcLoginAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = o.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = RedirectUri,
            ["scope"] = string.IsNullOrWhiteSpace(o.Scopes) ? "openid email profile" : o.Scopes,
            ["state"] = attempt.State,
            ["nonce"] = attempt.Nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        return QueryHelpers.AddQueryString(config.AuthorizationEndpoint, parameters);
    }

    /// <summary>La vuelta del proveedor: canjea el código, valida el id_token y resuelve a quién
    /// corresponde. Devuelve un ticket de un solo uso, no la sesión.</summary>
    public async Task<OidcCallbackResult> HandleCallbackAsync(string? code, string? state, CancellationToken ct)
    {
        var o = Options;
        if (!o.IsUsable) return OidcCallbackResult.Failed(OidcFailure.Config);

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return OidcCallbackResult.Failed(OidcFailure.Provider);
        }

        var attempt = await db.OidcLoginAttempts.FirstOrDefaultAsync(a => a.State == state, ct);

        // Un `state` que no existe, que ya se usó o que venció no se distingue: los tres casos son
        // el mismo intento de reproducir una vuelta ajena.
        if (attempt is null ||
            attempt.ConsumedAt is not null ||
            attempt.Ticket is not null ||
            DateTimeOffset.UtcNow - attempt.CreatedAt > OidcLoginAttempt.StateLifetime)
        {
            return OidcCallbackResult.Failed(OidcFailure.State);
        }

        var returnPath = attempt.ReturnPath;

        OpenIdConnectConfiguration config;
        try
        {
            config = await discovery.GetAsync(o.Authority, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer el descubrimiento de {Authority}", o.Authority);
            return OidcCallbackResult.Failed(OidcFailure.Config, returnPath);
        }

        string idToken;
        try
        {
            idToken = await ExchangeCodeAsync(config, o, code, attempt.CodeVerifier, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el canje del código con {Authority}", o.Authority);
            return OidcCallbackResult.Failed(OidcFailure.Provider, returnPath);
        }

        ClaimsFromToken claims;
        try
        {
            claims = ValidateIdToken(idToken, config, o, attempt.Nonce);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "id_token inválido de {Authority}", o.Authority);
            return OidcCallbackResult.Failed(OidcFailure.Provider, returnPath);
        }

        // El código del proveedor ya se canjeó: pase lo que pase de acá en adelante, este intento
        // se cierra. En el camino feliz lo cierra el canje del ticket; en los de error, acá mismo.
        async Task<OidcCallbackResult> RejectAsync(OidcFailure reason, string? shownEmail = null)
        {
            attempt.ConsumedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return OidcCallbackResult.Failed(reason, returnPath, shownEmail);
        }

        if (string.IsNullOrWhiteSpace(claims.Email) || claims.EmailVerified == false)
        {
            return await RejectAsync(OidcFailure.Email);
        }

        var email = claims.Email.Trim().ToLowerInvariant();

        var domains = o.Domains;
        if (domains.Count > 0 && !domains.Contains(email.Split('@').Last()))
        {
            return await RejectAsync(OidcFailure.Domain, email);
        }

        var (user, failure) = await ResolveUserAsync(claims.Issuer, claims.Subject, email, ct);

        // Nadie con ese email, pero el registro está abierto: en vez de rechazar, el intento queda
        // esperando a que esta persona le ponga nombre a su organización. Es la única diferencia
        // entre «entrar» y «darse de alta», y por eso comparte todo el flujo: el proveedor ya
        // verificó quién es.
        if (user is null && failure == OidcFailure.NoAccount && CanRegister(email))
        {
            attempt.PendingEmail = email;
            attempt.PendingIssuer = claims.Issuer;
            attempt.PendingSubject = claims.Subject;
            attempt.PendingName = claims.Name;
            attempt.Ticket = RandomToken(24);
            attempt.TicketIssuedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return new OidcCallbackResult(OidcFailure.None, attempt.Ticket, returnPath, email);
        }

        if (user is null) return await RejectAsync(failure, email);

        attempt.UserId = user.Id;
        attempt.Ticket = RandomToken(24);
        attempt.TicketIssuedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new OidcCallbackResult(OidcFailure.None, attempt.Ticket, returnPath, email);
    }

    /// <summary>Si esta dirección puede abrir una organización nueva. Dos condiciones y las dos
    /// son del que hospeda, no del que entra: que el registro esté abierto, y que el dominio esté
    /// permitido si se configuró una lista.</summary>
    private bool CanRegister(string email)
    {
        var options = tenancy.CurrentValue;
        return options.RegistrationOpen && options.DomainAllowed(email);
    }

    /// <summary>Qué hay del otro lado de un ticket: una persona que ya tiene cuenta, o una que
    /// está por crear su organización. No consume nada — el canje real lo hace
    /// <see cref="RedeemTicketAsync"/> o <see cref="RegisterAsync"/>—, porque mirar qué es no
    /// puede gastar el único uso que tiene.</summary>
    public async Task<OidcLoginAttempt?> PeekTicketAsync(string ticket, CancellationToken ct)
    {
        using var scope = OrganizationScope.UseSystem();

        var attempt = await db.OidcLoginAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Ticket == ticket, ct);

        if (attempt?.TicketIssuedAt is null || attempt.ConsumedAt is not null) return null;

        return DateTimeOffset.UtcNow - attempt.TicketIssuedAt.Value > attempt.LifetimeForTicket
            ? null
            : attempt;
    }

    /// <summary>Crea la organización de quien acaba de volver del proveedor sin tener cuenta, y la
    /// deja lista para entrar: la persona queda como administradora y con la cuenta del proveedor
    /// ya vinculada.
    ///
    /// La cuenta nace **sin contraseña**, y no es un olvido: entró con Google y va a seguir
    /// entrando con Google. Inventarle una contraseña sería una credencial más para perder, para
    /// filtrar y para rotar, a cambio de nada.</summary>
    public async Task<(User? User, string? Error)> RegisterAsync(
        string ticket,
        string organizationName,
        string? logoPath,
        OrganizationService organizations,
        CancellationToken ct)
    {
        using var scope = OrganizationScope.UseSystem();

        var attempt = await db.OidcLoginAttempts.FirstOrDefaultAsync(a => a.Ticket == ticket, ct);

        if (attempt is null ||
            !attempt.IsRegistration ||
            attempt.ConsumedAt is not null ||
            attempt.TicketIssuedAt is null ||
            DateTimeOffset.UtcNow - attempt.TicketIssuedAt.Value > OidcLoginAttempt.RegistrationTicketLifetime)
        {
            return (null, "El alta venció o ya se completó. Entrá de nuevo con el proveedor.");
        }

        var email = attempt.PendingEmail!;

        // Entre el callback y este POST pueden haber pasado quince minutos, y en el medio alguien
        // pudo dar de alta a esta persona a mano o completar el registro en otra pestaña.
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            attempt.ConsumedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return (null, "Esa dirección ya tiene cuenta. Volvé a entrar con el proveedor.");
        }

        if (!CanRegister(email)) return (null, "El registro está cerrado en esta instalación.");

        var (_, owner) = await organizations.CreateAsync(
            organizationName,
            email,
            attempt.PendingName ?? string.Empty,

            // Sin contraseña: esta cuenta entra por el proveedor. El login por contraseña rechaza
            // el hash vacío antes de llegar a BCrypt.
            string.Empty,
            logoPath: logoPath,
            ct: ct);

        db.ExternalIdentities.Add(new ExternalIdentity
        {
            OrganizationId = owner.OrganizationId,
            UserId = owner.Id,
            Issuer = attempt.PendingIssuer!,
            Subject = attempt.PendingSubject!,
            Email = email,
            LastLoginAt = DateTimeOffset.UtcNow
        });

        attempt.UserId = owner.Id;
        attempt.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return (owner, null);
    }

    /// <summary>Canjea el ticket por la persona. Un solo uso: dos pestañas con el mismo ticket
    /// solo entran una vez.</summary>
    public async Task<User?> RedeemTicketAsync(string ticket, CancellationToken ct)
    {
        // Sin filtro: el intento de login no pertenece a ninguna organización, pero la persona que
        // cuelga de él sí, y con el filtro puesto el Include la traería vacía.
        using var scope = OrganizationScope.UseSystem();

        var attempt = await db.OidcLoginAttempts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Ticket == ticket, ct);

        if (attempt?.User is null ||
            attempt.ConsumedAt is not null ||
            attempt.TicketIssuedAt is null ||
            DateTimeOffset.UtcNow - attempt.TicketIssuedAt.Value > OidcLoginAttempt.TicketLifetime)
        {
            return null;
        }

        // Desactivar a alguien entre el callback y el canje son dos segundos, pero la comprobación
        // cuesta nada y el caso es exactamente el que importa: revocar el acceso a alguien que se
        // está yendo.
        if (!attempt.User.IsActive) return null;

        attempt.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return attempt.User;
    }

    /// <summary>Para la pantalla de Configuración: si el descubrimiento responde y cuál es la URL
    /// de redirección que hay que registrar. Decir «guardado» no es lo mismo que decir
    /// «funciona», y la diferencia entre las dos cosas es un `redirect_uri_mismatch` que aparece
    /// recién cuando alguien intenta entrar.</summary>
    public async Task<OidcProbe> ProbeAsync(CancellationToken ct)
    {
        var o = Options;
        if (!o.IsComplete)
        {
            var faltan = new List<string>();
            if (string.IsNullOrWhiteSpace(o.Authority)) faltan.Add("la autoridad");
            if (string.IsNullOrWhiteSpace(o.ClientId)) faltan.Add("el ID de cliente");
            if (string.IsNullOrWhiteSpace(o.ClientSecret)) faltan.Add("el secreto de cliente");

            return new OidcProbe(o.Enabled, false, RedirectUri, null,
                $"Falta {string.Join(", ", faltan)}.");
        }

        try
        {
            var config = await discovery.GetAsync(o.Authority, ct);
            return new OidcProbe(o.Enabled, true, RedirectUri, config.Issuer, null);
        }
        catch (Exception ex)
        {
            return new OidcProbe(o.Enabled, true, RedirectUri, null,
                $"No se pudo leer {o.Authority.TrimEnd('/')}/.well-known/openid-configuration: {ex.Message}");
        }
    }

    /// <summary>La regla de vinculación, que es la decisión de diseño de todo esto: la identidad
    /// se ancla al `sub` del proveedor, y el email solo sirve la primera vez para encontrar a
    /// quién vincular.</summary>
    private async Task<(User? User, OidcFailure Failure)> ResolveUserAsync(
        string issuer, string subject, string email, CancellationToken ct)
    {
        // Todo este método corre sin filtro de organización: la vuelta del proveedor es anónima
        // —no hay token todavía— y justamente lo que se está resolviendo es a qué organización
        // pertenece quien entra.
        using var scope = OrganizationScope.UseSystem();

        var linkedAccounts = await db.ExternalIdentities
            .Include(i => i.User)
            .Where(i => i.Issuer == issuer && i.Subject == subject)
            .ToListAsync(ct);

        // La misma cuenta de Google puede estar vinculada a una persona en cada organización:
        // una cuenta por organización es exactamente eso. Elegir por el orden de las filas sería
        // meter a alguien en la empresa equivocada, así que se corta hasta que exista el paso de
        // elegir organización.
        if (linkedAccounts.Count > 1) return (null, OidcFailure.Ambiguous);

        var linked = linkedAccounts.SingleOrDefault();

        if (linked?.User is not null)
        {
            if (!linked.User.IsActive) return (null, OidcFailure.Inactive);

            linked.LastLoginAt = DateTimeOffset.UtcNow;
            linked.Email = email;
            return (linked.User, OidcFailure.None);
        }

        var candidates = await db.Users.Where(u => u.Email == email).ToListAsync(ct);
        if (candidates.Count > 1) return (null, OidcFailure.Ambiguous);

        var user = candidates.SingleOrDefault();

        // Acá está la decisión: no se crea a nadie. Alguien tiene que haber dado de alta a esta
        // persona con su rol, y recién entonces el proveedor le sirve para entrar.
        if (user is null) return (null, OidcFailure.NoAccount);
        if (!user.IsActive) return (null, OidcFailure.Inactive);

        // Ya hay otra cuenta del mismo proveedor colgada de esta persona. Pasa cuando la cuenta se
        // borró y se volvió a crear del otro lado: el email es el mismo, el `sub` no. Vincular la
        // segunda a ciegas es justo lo que no queremos, porque el mismo camino lo recorrería
        // alguien que heredó una dirección liberada. Lo resuelve un admin desvinculando.
        var otherFromSameIssuer = await db.ExternalIdentities
            .AnyAsync(i => i.UserId == user.Id && i.Issuer == issuer, ct);

        if (otherFromSameIssuer) return (null, OidcFailure.AlreadyLinked);

        db.ExternalIdentities.Add(new ExternalIdentity
        {
            // Explícita porque estamos en modo sistema: acá no hay organización ambiental que el
            // DbContext pueda estampar, y la vinculación va a la de la persona que se encontró.
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            Issuer = issuer,
            Subject = subject,
            Email = email,
            LastLoginAt = DateTimeOffset.UtcNow
        });

        return (user, OidcFailure.None);
    }

    private async Task<string> ExchangeCodeAsync(
        OpenIdConnectConfiguration config, OidcOptions o, string code, string verifier, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(HttpClientName);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = o.ClientId,
            ["client_secret"] = o.ClientSecret,
            ["code_verifier"] = verifier
        });

        var response = await client.PostAsync(config.TokenEndpoint, form, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"El proveedor devolvió {(int)response.StatusCode} al canjear el código: {payload}");
        }

        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("id_token", out var idToken) || idToken.GetString() is not { } value)
        {
            throw new InvalidOperationException("La respuesta del proveedor no trae id_token.");
        }

        return value;
    }

    private record ClaimsFromToken(string Issuer, string Subject, string? Email, bool? EmailVerified, string? Name);

    private static ClaimsFromToken ValidateIdToken(
        string idToken, OpenIdConnectConfiguration config, OidcOptions o, string expectedNonce)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config.Issuer,
            ValidateAudience = true,
            ValidAudience = o.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        handler.ValidateToken(idToken, parameters, out var validated);
        var jwt = (JwtSecurityToken)validated;

        var nonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(nonce ?? string.Empty), Encoding.UTF8.GetBytes(expectedNonce)))
        {
            throw new SecurityTokenValidationException("El nonce no corresponde a este intento de login.");
        }

        var subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? throw new SecurityTokenValidationException("El id_token no trae sub.");

        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

        // `email_verified` solo se exige cuando el proveedor lo manda. Google y Keycloak lo mandan
        // siempre; Microsoft Entra no lo emite, y exigirlo dejaría afuera a los tenants de
        // Microsoft 365 por una garantía que ahí la da el tenant. Cuando viene y dice que no, se
        // corta: vincular por un email sin verificar es regalar la cuenta a quien lo declare.
        bool? verified = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value is { } raw
            ? bool.TryParse(raw, out var parsed) && parsed
            : null;

        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

        return new ClaimsFromToken(jwt.Issuer, subject, email, verified, name);
    }

    /// <summary>Solo rutas de esta app. Una URL completa acá convierte a `/login` en un redirector
    /// abierto: un mail con el botón real de TaskAdmin que termina en otro dominio.</summary>
    private static string SafeReturnPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        if (!path.StartsWith('/')) return "/";
        if (path.StartsWith("//")) return "/";  // protocolo-relativo: //evil.com
        return path.Length > 300 ? "/" : path;
    }

    /// <summary>La pantalla de entrada, sin nada que explicar. Es a donde vuelve quien cancela.</summary>
    public string LoginRedirect() => $"{PublicBase}/login";

    public string FailureRedirect(OidcFailure failure, string? email) =>
        QueryHelpers.AddQueryString($"{PublicBase}/login", new Dictionary<string, string?>
        {
            ["error"] = failure switch
            {
                OidcFailure.Config => "config",
                OidcFailure.State => "estado",
                OidcFailure.Email => "email",
                OidcFailure.Domain => "dominio",
                OidcFailure.NoAccount => "sin_cuenta",
                OidcFailure.Inactive => "inactivo",
                OidcFailure.AlreadyLinked => "ya_vinculado",
                OidcFailure.Ambiguous => "varias_organizaciones",
                _ => "proveedor"
            },
            ["email"] = email
        });

    public string TicketRedirect(string ticket, string returnPath) =>
        QueryHelpers.AddQueryString($"{PublicBase}/login", new Dictionary<string, string?>
        {
            ["ticket"] = ticket,
            ["volver"] = returnPath == "/" ? null : returnPath
        });

    private static string RandomToken(int bytes) => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));
}
