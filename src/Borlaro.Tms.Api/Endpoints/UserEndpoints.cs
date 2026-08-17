using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;

namespace Borlaro.Tms.Api.Endpoints;

public record AdminUserDto(
    Guid Id,
    string Email,
    string Name,
    UserRole Role,
    bool IsActive,
    string TimeZoneId,
    TimeOnly CheckInTime,
    int WorkDaysMask,
    bool CheckInsEnabled,
    /// <summary>Los tres permisos por persona. Se comprueban **además** del permiso sobre el
    /// proyecto: encenderlos no mete a nadie en un tablero ajeno, solo decide qué puede hacer ahí
    /// donde ya entra. Nacen encendidos porque son restricciones, no concesiones.</summary>
    bool CanAssignTasks,
    bool CanSetDueDate,
    bool CanCreateTasks,
    /// <summary>Por dónde prefiere recibir el check-in. Null = automático.</summary>
    NotificationChannel? PreferredChannel,
    int OpenItems,
    DateTimeOffset CreatedAt,
    /// <summary>False en las cuentas que entran solo por el proveedor externo.</summary>
    bool HasPassword,
    /// <summary>La cuenta del proveedor externo vinculada, si hay alguna. Es lo que responde
    /// «¿por qué esta persona no puede entrar?» sin tener que mirar la base.</summary>
    string? ExternalLogin);

public record NewUserBody(
    [Required] string Email,
    [Required] string Name,
    /// <summary>Opcional cuando hay proveedor externo: la persona entra con la cuenta que ya
    /// tiene y no hace falta inventarle una contraseña que nadie va a usar.</summary>
    string? Password,
    UserRole Role,
    string? TimeZoneId,
    TimeOnly? CheckInTime,
    int? WorkDaysMask,
    bool? CheckInsEnabled);

/// <summary>Todos los campos son opcionales: se manda solo lo que cambia.</summary>
public record EditUserBody(
    string? Name,
    UserRole? Role,
    bool? IsActive,
    string? TimeZoneId,
    TimeOnly? CheckInTime,
    int? WorkDaysMask,
    bool? CheckInsEnabled,
    bool? CanAssignTasks = null,
    bool? CanSetDueDate = null,
    bool? CanCreateTasks = null,
    /// <summary>Canal preferido. `ClearPreferredChannel` es lo que permite volver a automático:
    /// con solo el nulable no se distingue «no lo mandé» de «lo quiero vacío».</summary>
    NotificationChannel? PreferredChannel = null,
    bool ClearPreferredChannel = false);

public record NewPasswordBody([Required] string Password);

/// <summary>Alta y gestión de personas, para el rol Admin.
///
/// Dos reglas que no son negociables acá: nunca se borra un usuario —se desactiva, porque su
/// nombre cuelga de tareas, comentarios y transcripts— y la instancia no puede quedarse sin
/// ningún admin activo, ni por desactivación ni por cambio de rol. Sin esa segunda regla, un
/// click deja a todos afuera de la configuración y hay que entrar por la base a arreglarlo.</summary>
public static class UserEndpoints
{
    public const int MinPasswordLength = 10;

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Usuarios")
            .RequireAuthorization(Policies.IsAdmin);

        users.MapGet("/", async (BorlaroTmsDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Users
                .AsNoTracking()
                .OrderByDescending(u => u.IsActive)
                .ThenBy(u => u.Name)
                .Select(u => new AdminUserDto(
                    u.Id, u.Email, u.Name, u.Role, u.IsActive, u.TimeZoneId, u.CheckInTime,
                    u.WorkDaysMask, u.CheckInsEnabled,
                    u.CanAssignTasks, u.CanSetDueDate, u.CanCreateTasks, u.PreferredChannel,
                    db.WorkItems.Count(i => i.AssigneeId == u.Id && i.ClosedAt == null),
                    u.CreatedAt,
                    u.PasswordHash != "",
                    db.ExternalIdentities.Where(x => x.UserId == u.Id)
                        .Select(x => x.Email).FirstOrDefault()))
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("ListAllUsers");

        users.MapPost("/", async (
            NewUserBody body,
            BorlaroTmsDbContext db,
            IConfiguration configuration,
            IOptionsMonitor<OidcOptions> oidc,
            CancellationToken ct) =>
        {
            // El rol de operador de plataforma no se reparte desde adentro de una organización:
            // si un administrador pudiera asignárselo, el aislamiento entre empresas duraría lo
            // que tarde alguien en editar el JSON del alta. Esa cuenta la crea el arranque.
            if (body.Role == UserRole.PlatformOperator)
            {
                return Results.Problem("Ese rol no se puede asignar desde una organización.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var email = body.Email.Trim().ToLowerInvariant();

            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                return Results.Problem("Ya existe un usuario con ese email.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Sin contraseña la cuenta entra solo por el proveedor externo. Permitirlo con el
            // proveedor apagado crearía una persona que no puede entrar por ningún lado, y el
            // síntoma aparecería recién cuando lo intentara.
            if (string.IsNullOrEmpty(body.Password))
            {
                if (!oidc.CurrentValue.IsUsable)
                {
                    return Results.Problem(
                        "Sin contraseña esta persona no podría entrar: el proveedor externo está " +
                        "apagado o incompleto. Cargá una contraseña, o activá «Entrar con un " +
                        "proveedor externo» en Configuración.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }
            else if (body.Password.Length < MinPasswordLength)
            {
                return Results.Problem($"La contraseña debe tener al menos {MinPasswordLength} caracteres.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!IsKnownTimeZone(body.TimeZoneId))
            {
                return Results.Problem($"«{body.TimeZoneId}» no es una zona horaria conocida.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Los defaults salen de la configuración editable, no de constantes en el código:
            // es lo que hace que el ajuste «hora del check-in por defecto» signifique algo.
            var defaultTime = TimeOnly.TryParse(configuration["Defaults:CheckInTime"], out var parsed)
                ? parsed
                : new TimeOnly(9, 0);

            var defaultDays = int.TryParse(configuration["Defaults:WorkDaysMask"], out var mask)
                ? mask
                : 0b0111110;

            var user = new User
            {
                Email = email,
                Name = body.Name.Trim(),
                PasswordHash = string.IsNullOrEmpty(body.Password)
                    ? string.Empty
                    : BCrypt.Net.BCrypt.HashPassword(body.Password),
                Role = body.Role,
                TimeZoneId = body.TimeZoneId ?? "UTC",
                CheckInTime = body.CheckInTime ?? defaultTime,
                WorkDaysMask = body.WorkDaysMask ?? defaultDays,
                CheckInsEnabled = body.CheckInsEnabled ?? true
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/users/{user.Id}", await OneAsync(db, user.Id, ct));
        })
        .WithName("AdminCreateUser");

        users.MapPatch("/{id:guid}", async (
            Guid id,
            EditUserBody body,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            if (body.Role == UserRole.PlatformOperator)
            {
                return Results.Problem("Ese rol no se puede asignar desde una organización.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            var losesAdmin = (body.Role is not null && body.Role != UserRole.Admin && user.Role == UserRole.Admin)
                          || (body.IsActive == false && user.Role == UserRole.Admin);

            if (losesAdmin && !await AnotherAdminRemainsAsync(db, user.Id, ct))
            {
                return Results.Problem(
                    "Es el único administrador activo. Nombrá otro antes de cambiarle el rol o desactivarlo.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (body.IsActive == false && id == principal.UserId())
            {
                return Results.Problem("No podés desactivar tu propia cuenta.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Bajarle el rol a quien lidera proyectos dejaría un líder que hoy no podría ser
            // designado: la regla valdría al designar y no después, que es como se degradan las
            // reglas hasta dejar de significar algo.
            if (body.Role is UserRole nuevo && !ProjectService.CanLead(nuevo))
            {
                var lidera = await db.ProjectMembers
                    .Where(m => m.UserId == id && m.Role == ProjectRole.Lead)
                    .Select(m => m.Project!.Key)
                    .ToListAsync(ct);

                if (lidera.Count > 0)
                {
                    return Results.Problem(
                        $"{user.Name} lidera {string.Join(", ", lidera)}. Pasale el liderazgo a " +
                        "otra persona antes de bajarle el rol, o esos proyectos quedan con un " +
                        "responsable que ya no puede serlo.",
                        statusCode: StatusCodes.Status409Conflict);
                }
            }

            if (body.TimeZoneId is not null && !IsKnownTimeZone(body.TimeZoneId))
            {
                return Results.Problem($"«{body.TimeZoneId}» no es una zona horaria conocida.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (body.Name is not null) user.Name = body.Name.Trim();
            if (body.Role is not null) user.Role = body.Role.Value;
            if (body.IsActive is not null) user.IsActive = body.IsActive.Value;
            if (body.TimeZoneId is not null) user.TimeZoneId = body.TimeZoneId;
            if (body.CheckInTime is not null) user.CheckInTime = body.CheckInTime.Value;
            if (body.WorkDaysMask is not null) user.WorkDaysMask = body.WorkDaysMask.Value;
            if (body.CheckInsEnabled is not null) user.CheckInsEnabled = body.CheckInsEnabled.Value;

            // Los tres permisos por persona. Se editan desde la misma pantalla que el resto del
            // perfil: separarlos en otro lugar los volvería invisibles, y un permiso que nadie
            // encuentra es igual a uno que no existe.
            if (body.CanAssignTasks is not null) user.CanAssignTasks = body.CanAssignTasks.Value;
            if (body.CanSetDueDate is not null) user.CanSetDueDate = body.CanSetDueDate.Value;
            if (body.CanCreateTasks is not null) user.CanCreateTasks = body.CanCreateTasks.Value;

            // El correo no es un canal personal: es el último recurso de la escalera, y elegirlo
            // como preferido dejaría a alguien sin los dos primeros peldaños sin querer.
            if (body.ClearPreferredChannel) user.PreferredChannel = null;
            else if (body.PreferredChannel is { } canal)
            {
                if (canal is NotificationChannel.Email or NotificationChannel.InApp)
                {
                    return Results.Problem(
                        "El correo no se elige como canal preferido: ya es el último recurso de todos.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                user.PreferredChannel = canal;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(await OneAsync(db, user.Id, ct));
        })
        .WithName("AdminEditUser");

        users.MapPost("/{id:guid}/password", async (
            Guid id,
            NewPasswordBody body,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            if (body.Password.Length < MinPasswordLength)
            {
                return Results.Problem($"La contraseña debe tener al menos {MinPasswordLength} caracteres.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password);
            await db.SaveChangesAsync(ct);

            // Los tokens ya emitidos siguen siendo válidos hasta que venzan: revocarlos requiere
            // una lista de sesiones que hoy no existe. Queda anotado como pendiente, no oculto.
            return Results.NoContent();
        })
        .WithName("AdminResetPassword");

        users.MapDelete("/{id:guid}/external", async (
            Guid id,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            // La salida para el caso en que la cuenta del proveedor cambió de identidad —se borró
            // y se volvió a crear, y el `sub` es otro—. Sin esto la persona queda afuera y la
            // única solución es entrar por la base. Al desvincular, el próximo login vuelve a
            // vincular por email, que es exactamente lo que hay que hacer.
            var links = await db.ExternalIdentities.Where(i => i.UserId == id).ToListAsync(ct);
            if (links.Count == 0) return Results.NoContent();

            db.ExternalIdentities.RemoveRange(links);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("AdminUnlinkExternalLogin");

        users.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            if (id == principal.UserId())
            {
                return Results.Problem("No podés desactivar tu propia cuenta.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (user.Role == UserRole.Admin && !await AnotherAdminRemainsAsync(db, user.Id, ct))
            {
                return Results.Problem("Es el único administrador activo.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Desactivar, no borrar: su nombre cuelga de tareas, comentarios y transcripts, y un
            // borrado dejaría el historial hablando de un fantasma.
            user.IsActive = false;
            user.CheckInsEnabled = false;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("AdminDeactivateUser");

        /// Borrado definitivo, solo para cuentas sin rastro: la que se cargó con el mail mal
        /// escrito, la de prueba. Si la persona trabajó —tareas, comentarios, revisiones,
        /// check-ins— no se borra, porque el historial pasaría a nombrar a un fantasma; para eso
        /// está desactivar.
        users.MapDelete("/{id:guid}/definitivo", async (
            Guid id,
            ClaimsPrincipal principal,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            if (id == principal.UserId())
            {
                return Results.Problem("No podés borrar tu propia cuenta.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (user.Role == UserRole.Admin && !await AnotherAdminRemainsAsync(db, user.Id, ct))
            {
                return Results.Problem("Es el único administrador activo.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var rastro = await HistoryOfAsync(db, id, ct);
            if (rastro.Count > 0)
            {
                return Results.Problem(
                    $"{user.Name} no se puede borrar porque dejó historial: " +
                    string.Join(", ", rastro.Select(r => $"{r.Value} {r.Key}")) + ". " +
                    "Desactivala en su lugar: deja de entrar y de recibir check-ins, y el " +
                    "historial sigue diciendo quién hizo qué.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Sin rastro, lo único que puede colgar son sus propios dispositivos y su ficha.
            await db.DeviceRegistrations.Where(d => d.UserId == id).ExecuteDeleteAsync(ct);
            await db.ExternalIdentities.Where(e => e.UserId == id).ExecuteDeleteAsync(ct);
            await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct);

            return Results.Ok(new { deleted = user.Name });
        })
        .WithName("AdminDeleteUser");

        return app;
    }

    /// <summary>Qué dejó esta persona en el sistema. Se cuenta en vez de intentar borrar y ver
    /// qué explota: el mensaje puede decir exactamente por qué no se puede.</summary>
    private static async Task<Dictionary<string, int>> HistoryOfAsync(
        BorlaroTmsDbContext db,
        Guid userId,
        CancellationToken ct)
    {
        var rastro = new Dictionary<string, int>();

        void Add(string what, int count)
        {
            if (count > 0) rastro[what] = count;
        }

        Add("tareas asignadas", await db.WorkItems.CountAsync(i => i.AssigneeId == userId, ct));
        Add("tareas creadas", await db.WorkItems.CountAsync(i => i.ReporterId == userId, ct));
        Add("comentarios", await db.WorkItemComments.CountAsync(c => c.AuthorId == userId, ct));
        Add("cambios en el historial", await db.WorkItemEvents.CountAsync(e => e.ActorId == userId, ct));
        Add("check-ins", await db.CheckIns.CountAsync(c => c.UserId == userId, ct));
        Add("revisiones", await db.ReviewRounds.CountAsync(r => r.ReviewerId == userId, ct));
        Add("entregables subidos", await db.DeliverableVersions.CountAsync(v => v.UploadedById == userId, ct));
        Add("mensajes", await db.DirectMessages.CountAsync(m => m.FromUserId == userId || m.ToUserId == userId, ct));
        Add("proyectos que lidera", await db.ProjectMembers.CountAsync(m => m.UserId == userId, ct));

        return rastro;
    }

    private static Task<bool> AnotherAdminRemainsAsync(BorlaroTmsDbContext db, Guid excludingId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id != excludingId && u.IsActive && u.Role == UserRole.Admin, ct);

    /// <summary>Una zona horaria inválida no rompe nada al guardarse, pero silencia los check-ins
    /// de esa persona: el scheduler cae a UTC y le escribe a las 9 de otro huso. Se valida acá.</summary>
    private static bool IsKnownTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true;
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static async Task<AdminUserDto> OneAsync(BorlaroTmsDbContext db, Guid id, CancellationToken ct) =>
        await db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.Name, u.Role, u.IsActive, u.TimeZoneId, u.CheckInTime,
                u.WorkDaysMask, u.CheckInsEnabled,
                u.CanAssignTasks, u.CanSetDueDate, u.CanCreateTasks, u.PreferredChannel,
                db.WorkItems.Count(i => i.AssigneeId == u.Id && i.ClosedAt == null),
                u.CreatedAt,
                u.PasswordHash != "",
                db.ExternalIdentities.Where(x => x.UserId == u.Id)
                    .Select(x => x.Email).FirstOrDefault()))
            .FirstAsync(ct);
}
