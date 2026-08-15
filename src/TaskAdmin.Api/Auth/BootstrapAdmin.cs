using Microsoft.EntityFrameworkCore;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Api.Auth;

public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminName { get; set; } = "Administrador";
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Nombre de la organización que se crea junto con el primer administrador. En una
    /// instalación de una sola empresa es lo que aparecerá en pantalla cuando haya dónde
    /// mostrarlo; en una instalación multiempresa es simplemente la primera.</summary>
    public string OrganizationName { get; set; } = "Mi organización";
}

/// <summary>Crea la primera organización y su administrador. Sin esto la instancia queda
/// inaccesible: crear usuarios exige rol Admin, y sin ningún usuario nadie puede autenticarse
/// para pedirlo.
///
/// Deliberadamente NO hay credenciales por defecto: si no se configuran, la app arranca y avisa
/// cómo hacerlo. Un admin/admin de fábrica es la forma más común de que una instalación quede
/// expuesta el día que alguien la publica sin revisar.</summary>
public static class BootstrapAdmin
{
    public static async Task EnsureAsync(
        TaskAdminDbContext db,
        OrganizationService organizations,
        BootstrapOptions options,
        ILogger logger,
        CancellationToken ct = default)
    {
        // Sin filtro: acá se pregunta si hay *alguien* en toda la instalación, no en una
        // organización. Con el filtro puesto la respuesta sería siempre «no hay nadie» y esto
        // intentaría crear el administrador en cada arranque.
        using var scope = OrganizationScope.UseSystem();

        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning(
                "No hay usuarios y no se configuró el administrador inicial. La instancia no es " +
                "accesible hasta definir Bootstrap:AdminEmail y Bootstrap:AdminPassword " +
                "(user-secrets en desarrollo, variables Bootstrap__AdminEmail / Bootstrap__AdminPassword " +
                "en producción) y reiniciar.");
            return;
        }

        if (options.AdminPassword.Length < 10)
        {
            logger.LogError(
                "Bootstrap:AdminPassword tiene menos de 10 caracteres. No se creó el administrador.");
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(options.AdminPassword);
        var email = options.AdminEmail.Trim().ToLowerInvariant();

        // Si ya hay una organización de cliente —una instalación que se migró desde la versión de
        // una sola empresa— el administrador entra en esa, y no en una segunda que nadie pidió.
        var existing = await db.Organizations
            .Where(o => !o.IsPlatform)
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            db.Users.Add(new User
            {
                OrganizationId = existing.Id,
                Email = email,
                Name = options.AdminName,
                PasswordHash = hash,
                Role = UserRole.Admin,
                TimeZoneId = options.TimeZoneId
            });

            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Administrador inicial creado en «{Organization}»: {Email}", existing.Name, email);
            return;
        }

        var (organization, admin) = await organizations.CreateAsync(
            options.OrganizationName, email, options.AdminName, hash,
            timeZoneId: options.TimeZoneId, ct: ct);

        logger.LogInformation(
            "Organización «{Organization}» y administrador inicial creados: {Email}",
            organization.Name, admin.Email);
    }
}

/// <summary>Crea la cuenta de quien opera la instalación, en su propia organización.
///
/// Vive aparte del administrador inicial porque son dos personas distintas y dos decisiones
/// distintas: en una instalación de una sola empresa el operador no existe, y en una multiempresa
/// el operador no es el administrador de ninguna. Confundirlos es exactamente lo que hace
/// imposible después decir «este administrador no puede ver la organización de al lado».</summary>
public static class BootstrapOperator
{
    public const string PlatformOrganizationName = "Plataforma";

    public static async Task EnsureAsync(
        TaskAdminDbContext db,
        PlatformOptions options,
        TenancyOptions tenancy,
        ILogger logger,
        CancellationToken ct = default)
    {
        // En una instalación de una sola empresa no hay a quién operar: no existe la consola, no
        // se pueden crear organizaciones, y una cuenta con ese poder sería solo una llave más.
        if (!tenancy.IsMulti) return;

        if (string.IsNullOrWhiteSpace(options.OperatorEmail) ||
            string.IsNullOrWhiteSpace(options.OperatorPassword))
        {
            logger.LogWarning(
                "Modo multiempresa sin operador configurado: nadie puede dar de alta organizaciones " +
                "desde la consola. Definí Platform__OperatorEmail y Platform__OperatorPassword.");
            return;
        }

        if (options.OperatorPassword.Length < 10)
        {
            logger.LogError("Platform:OperatorPassword tiene menos de 10 caracteres. No se creó el operador.");
            return;
        }

        using var scope = OrganizationScope.UseSystem();

        var platform = await db.Organizations.FirstOrDefaultAsync(o => o.IsPlatform, ct);

        if (platform is null)
        {
            platform = new Organization
            {
                Name = PlatformOrganizationName,
                Slug = "plataforma",
                IsPlatform = true
            };

            db.Organizations.Add(platform);
        }

        var email = options.OperatorEmail.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.OrganizationId == platform.Id && u.Email == email, ct))
        {
            return;
        }

        db.Users.Add(new User
        {
            OrganizationId = platform.Id,
            Email = email,
            Name = options.OperatorName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.OperatorPassword),
            Role = UserRole.PlatformOperator,

            // El operador no ejecuta trabajo: no hay de qué preguntarle cada mañana.
            CheckInsEnabled = false
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Operador de plataforma creado: {Email}", email);
    }
}

public class PlatformOptions
{
    public const string SectionName = "Platform";

    public string? OperatorEmail { get; set; }
    public string? OperatorPassword { get; set; }
    public string OperatorName { get; set; } = "Operador";
}
