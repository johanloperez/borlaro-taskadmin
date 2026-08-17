using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Api.Localization;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Api.Auth;

public record LoginRequest([Required] string Email, [Required] string Password);

public record DeviceExchangeRequest([Required] string DeviceToken, string? AppVersion);

public record CreateUserRequest(
    [Required] string Email,
    [Required] string Name,
    [Required] string Password,
    UserRole Role,
    string? TimeZoneId,
    TimeOnly? CheckInTime);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User);

public record UserResponse(
    Guid Id,
    string Email,
    string Name,
    UserRole Role,
    string TimeZoneId,
    TimeOnly CheckInTime,
    /// <summary>Null = el idioma del navegador. Solo trae valor cuando la persona eligió uno.</summary>
    string? Language,
    /// <summary>El nombre de la empresa a la que pertenece esta persona. Es lo que la interfaz
    /// muestra al entrar, para que quede claro en qué organización se está trabajando.</summary>
    string? OrganizationName,
    /// <summary>La organización de la persona. Sirve para pedir su logo.</summary>
    Guid OrganizationId,
    /// <summary>Ruta del logo de la organización en el almacenamiento. Null = no tiene logo.</summary>
    string? LogoPath,
    /// <summary>Los permisos por persona, en la sesión. La interfaz los necesita para no dibujar
    /// controles que siempre van a terminar en 403 — un botón que nunca funciona es peor que uno
    /// que no está. El corte de verdad sigue estando en el servidor.</summary>
    bool CanAssignTasks,
    bool CanSetDueDate,
    bool CanCreateTasks);

public record LanguageBody(string? Language);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            HttpContext http,
            BorlaroTmsDbContext db,
            TokenService tokens,
            CancellationToken ct) =>
        {
            var lang = http.Language();
            var email = request.Email.Trim().ToLowerInvariant();

            // El login es anónimo: todavía no hay organización en el contexto, así que la
            // búsqueda tiene que atravesar la instalación para encontrar a quién pertenece esta
            // dirección. Es el único punto donde eso está justificado.
            List<User> candidates;
            using (OrganizationScope.UseSystem())
            {
                candidates = await db.Users.Where(u => u.Email == email).ToListAsync(ct);
            }

            // Con una cuenta por organización, la misma dirección puede existir en varias
            // empresas, y entonces el email solo no alcanza para saber a cuál entrar. Hasta que
            // exista el paso de elegir organización, esto se corta acá en vez de elegir una por
            // el orden de las filas.
            if (candidates.Count > 1)
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.AccountInManyOrganizations),
                    statusCode: StatusCodes.Status409Conflict);
            }

            var user = candidates.SingleOrDefault();

            // Mismo mensaje y mismo camino para usuario inexistente, cuenta sin contraseña y
            // contraseña incorrecta: distinguirlos permite enumerar cuentas válidas —y, con el
            // proveedor externo configurado, permitiría además saber quién entra por dónde—.
            // La cuenta sin contraseña se corta antes de llegar a BCrypt: una cadena vacía no es
            // un hash válido y `Verify` tira excepción en vez de devolver false.
            if (user is null ||
                !user.IsActive ||
                string.IsNullOrEmpty(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.InvalidCredentials),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // Después de verificar la contraseña y no antes: el estado de la organización no es
            // algo que deba poder averiguar quien no tiene credenciales.
            if (await SuspendedAsync(db, user, ct))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.OrganizationSuspended),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var (token, expiresAt) = tokens.Issue(user);
            return Results.Ok(new AuthResponse(token, expiresAt, await ToResponseAsync(db, user, ct)));
        })
        .WithName("Login")
        .AllowAnonymous();

        group.MapPost("/users", async (
            CreateUserRequest request,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            if (request.Role == UserRole.PlatformOperator)
            {
                return Results.Problem("Ese rol no se puede asignar desde una organización.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var email = request.Email.Trim().ToLowerInvariant();

            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return Results.Problem("Ya existe un usuario con ese email.", statusCode: StatusCodes.Status409Conflict);
            }

            if (request.Password.Length < 10)
            {
                return Results.Problem("La contraseña debe tener al menos 10 caracteres.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var user = new User
            {
                Email = email,
                Name = request.Name.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                TimeZoneId = request.TimeZoneId ?? "UTC",
                CheckInTime = request.CheckInTime ?? new TimeOnly(9, 0)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/auth/users/{user.Id}", await ToResponseAsync(db, user, ct));
        })
        .WithName("CreateUser")
        .RequireAuthorization(Policies.IsAdmin);

        group.MapGet("/users", async (BorlaroTmsDbContext db, CancellationToken ct) =>
        {
            // Lista mínima para poder asignar trabajo y elegir revisor. No expone hashes ni
            // configuración de check-in: para eso está /me y, más adelante, el perfil.
            var users = await db.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.Name, u.Email, u.Role })
                .ToListAsync(ct);

            return Results.Ok(users);
        })
        .WithName("ListUsers")
        .RequireAuthorization();

        /// Canjea el token de un dispositivo por una sesión nueva.
        ///
        /// Es lo que hace que la app de escritorio no cierre sesión: entra una vez con usuario y
        /// contraseña, guarda un token de dispositivo que no vence, y con eso renueva sola. La
        /// diferencia con dejar el token de sesión eterno es que este se revoca desde la web sin
        /// tocar la contraseña de la persona.
        group.MapPost("/device", async (
            DeviceExchangeRequest request,
            HttpContext http,
            BorlaroTmsDbContext db,
            TokenService tokens,
            CancellationToken ct) =>
        {
            var lang = http.Language();

            // Igual que el login: el token del dispositivo es lo único que identifica al que
            // llama, y es único en toda la instalación, así que la búsqueda va sin filtro.
            DeviceRegistration? device;
            using (OrganizationScope.UseSystem())
            {
                device = await db.DeviceRegistrations
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Token == request.DeviceToken && d.RevokedAt == null, ct);
            }

            if (device?.User is null || !device.User.IsActive)
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.DeviceUnlinked),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (await SuspendedAsync(db, device.User, ct))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.OrganizationSuspended),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // Renovar cuenta como señal de vida: si la app está pidiendo sesión, está corriendo.
            device.LastHeartbeatAt = DateTimeOffset.UtcNow;
            if (request.AppVersion is not null) device.AppVersion = request.AppVersion;

            using (OrganizationScope.Use(device.OrganizationId))
            {
                await db.SaveChangesAsync(ct);
            }

            var (token, expiresAt) = tokens.Issue(device.User);
            return Results.Ok(new AuthResponse(token, expiresAt, await ToResponseAsync(db, device.User, ct)));
        })
        .WithName("ExchangeDeviceToken")
        .AllowAnonymous();

        group.MapGet("/me", async (
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var id = principal.UserId();
            if (id is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync([id.Value], ct);
            return user is null ? Results.Unauthorized() : Results.Ok(await ToResponseAsync(db, user, ct));
        })
        .WithName("Me")
        .RequireAuthorization();

        /// En qué idioma le habla el sistema a esta persona.
        ///
        /// Se guarda en el servidor y no solo en el navegador porque los emails de la escalera se
        /// arman del lado del servidor, sin navegador del cual deducirlo: sin esta fila, alguien
        /// que usa la app en portugués recibiría los avisos en castellano.
        group.MapPut("/me/idioma", async (
            LanguageBody body,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var id = principal.UserId();
            if (id is null) return Results.Unauthorized();

            var language = body.Language?.Trim().ToLowerInvariant();

            // Vacío devuelve a «el del navegador», que es el estado inicial de todo el mundo.
            if (string.IsNullOrEmpty(language)) language = null;

            if (language is not null && !SupportedLanguages.Contains(language))
            {
                return Results.Problem($"«{language}» no es un idioma disponible.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id.Value, ct);
            if (user is null) return Results.Unauthorized();

            user.Language = language;
            await db.SaveChangesAsync(ct);

            return Results.Ok(await ToResponseAsync(db, user, ct));
        })
        .WithName("SetLanguage")
        .RequireAuthorization();

        return app;
    }

    internal static async Task<UserResponse> ToResponseAsync(
        BorlaroTmsDbContext db, User u, CancellationToken ct)
    {
        // Las organizaciones no llevan el filtro de organización (son su raíz), así que esto se
        // puede consultar desde cualquier scope — incluso desde el login, que corre sin sesión.
        var organization = await db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == u.OrganizationId)
            .Select(o => new { o.Name, o.LogoPath })
            .FirstOrDefaultAsync(ct);

        return new(
            u.Id, u.Email, u.Name, u.Role, u.TimeZoneId, u.CheckInTime, u.Language,
            organization?.Name, u.OrganizationId, organization?.LogoPath,
            u.CanAssignTasks, u.CanSetDueDate, u.CanCreateTasks);
    }

    /// <summary>Los idiomas que habla el producto. Una lista corta y explícita: aceptar cualquier
    /// código dejaría entrar «xx» y el resultado sería una interfaz a medio traducir.</summary>
    internal static readonly string[] SupportedLanguages = ["es", "en", "pt"];

    /// <summary>Si la organización de esta persona está suspendida. Se consulta en el momento de
    /// emitir la sesión: el token dura lo que dura, y suspender una organización tiene que cortar
    /// los ingresos nuevos aunque haya sesiones vivas.</summary>
    internal static async Task<bool> SuspendedAsync(BorlaroTmsDbContext db, User user, CancellationToken ct)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct);

        return organization is null || organization.IsSuspended;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>La organización de la sesión, tal como vino firmada en el token.</summary>
    public static Guid? OrganizationId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(TokenService.OrganizationClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
