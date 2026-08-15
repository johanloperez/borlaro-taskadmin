using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Seeding;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Infrastructure.Services;

/// <summary>Dar de alta una organización: la fila, su primer administrador y las plantillas de
/// fábrica. Vive en un solo lugar porque hay tres caminos que llegan acá —el arranque, la consola
/// del operador y el registro con el proveedor de identidad— y una organización a la que le
/// faltara el seed sería una empresa nueva que no puede crear su primer proyecto.</summary>
public class OrganizationService(TaskAdminDbContext db)
{
    /// <summary>Crea la organización con su primer administrador.
    ///
    /// Todo adentro de una transacción: una organización sin dueño es inaccesible —crear usuarios
    /// exige ser admin— y habría que entrar a la base a mano para arreglarla.</summary>
    public async Task<(Organization Organization, User Owner)> CreateAsync(
        string name,
        string ownerEmail,
        string ownerName,
        string ownerPasswordHash,
        string? slug = null,
        string timeZoneId = "UTC",
        string? logoPath = null,
        CancellationToken ct = default)
    {
        // Sin filtro: acá se está creando una organización, así que no hay ninguna activa desde la
        // cual mirar, y el chequeo de slug tiene que ver toda la instalación.
        using var scope = OrganizationScope.UseSystem();

        var email = ownerEmail.Trim().ToLowerInvariant();
        if (!email.Contains('@')) throw new DomainException("El email del administrador no es válido.");

        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("La organización necesita un nombre.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var organization = new Organization
        {
            Name = name.Trim(),
            Slug = await FreeSlugAsync(slug is null ? Slugify(name) : Slugify(slug), ct),
            LogoPath = string.IsNullOrWhiteSpace(logoPath) ? null : logoPath
        };

        db.Organizations.Add(organization);

        var owner = new User
        {
            OrganizationId = organization.Id,
            Email = email,
            Name = string.IsNullOrWhiteSpace(ownerName) ? email.Split('@')[0] : ownerName.Trim(),
            PasswordHash = ownerPasswordHash,
            Role = UserRole.Admin,
            TimeZoneId = timeZoneId,

            // El primer administrador no recibe check-ins: el agente le habla a quien ejecuta el
            // trabajo, y el día uno esta persona todavía no tiene ninguno.
            CheckInsEnabled = false
        };

        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);

        await ProjectTemplateSeeder.SeedAsync(db, organization.Id, ct);

        await tx.CommitAsync(ct);

        return (organization, owner);
    }

    /// <summary>Borra una organización y absolutamente todo lo suyo. No hay vuelta atrás.
    ///
    /// Devuelve las rutas de los archivos que quedaron sin dueño, para que quien llama los borre
    /// **después** de que la transacción haya cerrado bien. Si se borraran acá adentro y la
    /// transacción fallara, los archivos ya no estarían y las filas seguirían apuntándolos.
    ///
    /// Sobre el orden de abajo: las 27 tablas que apuntan a `Organizations` lo hacen con
    /// `ON DELETE RESTRICT`, así que hay que vaciarlas de hijas a madres. Eso es frágil de
    /// mantener —una tabla nueva que nadie agregue acá queda afuera— y por eso el `RESTRICT` es
    /// además la red: si algo quedó, el borrado de la organización falla y la transacción entera
    /// se deshace. Falla ruidosamente y sin borrar nada, que es exactamente el modo de falla que
    /// se quiere en una operación irreversible.</summary>
    public async Task<IReadOnlyList<string>> DeleteAsync(Guid organizationId, CancellationToken ct = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new InvalidOperationException("La organización no existe.");

        // La organización de la plataforma es donde vive la cuenta del operador. Borrarla dejaría
        // la instalación sin nadie que pueda administrarla, y sin forma de arreglarlo desde la
        // interfaz.
        if (organization.IsPlatform)
        {
            throw new InvalidOperationException(
                "La organización de la plataforma no se puede borrar: es donde vive la cuenta que administra la instalación.");
        }

        // Suspendida primero, a propósito. Convierte un botón irreversible en dos pasos separados
        // en el tiempo: para borrar hay que haber cortado el acceso antes y haber visto qué pasa.
        if (!organization.IsSuspended)
        {
            throw new InvalidOperationException(
                "Antes de borrar hay que suspender la organización. Es un paso a propósito: da lugar a arrepentirse.");
        }

        // El alcance se abre a la organización que se va a borrar; si no, el filtro global no
        // dejaría ver —ni borrar— nada de ella.
        using var scope = OrganizationScope.Use(organizationId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Las rutas se leen antes de borrar las filas: después no hay de dónde sacarlas.
        var archivos = await db.DeliverableVersions
            .Where(v => v.FilePath != null)
            .Select(v => v.FilePath!)
            .ToListAsync(ct);

        if (organization.LogoPath is not null) archivos.Add(organization.LogoPath);

        // De hijas a madres. Cada línea es una tabla con OrganizationId; el orden lo dictan las
        // claves foráneas entre ellas, no el capricho.
        await db.WorkItemLabels.ExecuteDeleteAsync(ct);
        await db.WorkItemDependencies.ExecuteDeleteAsync(ct);
        await db.WorkItemComments.ExecuteDeleteAsync(ct);
        await db.WorkItemEvents.ExecuteDeleteAsync(ct);
        await db.Blockers.ExecuteDeleteAsync(ct);
        await db.DeliverableVersions.ExecuteDeleteAsync(ct);
        await db.Deliverables.ExecuteDeleteAsync(ct);
        await db.ReviewRounds.ExecuteDeleteAsync(ct);
        await db.AgentActions.ExecuteDeleteAsync(ct);
        await db.NotificationAttempts.ExecuteDeleteAsync(ct);
        await db.Notifications.ExecuteDeleteAsync(ct);
        await db.DirectMessages.ExecuteDeleteAsync(ct);
        await db.CheckIns.ExecuteDeleteAsync(ct);
        await db.DeviceRegistrations.ExecuteDeleteAsync(ct);
        await db.ExternalIdentities.ExecuteDeleteAsync(ct);
        await db.WorkItems.ExecuteDeleteAsync(ct);
        await db.ProjectMembers.ExecuteDeleteAsync(ct);
        await db.RepoLinks.ExecuteDeleteAsync(ct);
        await db.CustomFieldDefs.ExecuteDeleteAsync(ct);
        await db.Labels.ExecuteDeleteAsync(ct);
        await db.Projects.ExecuteDeleteAsync(ct);
        await db.WorkflowTransitions.ExecuteDeleteAsync(ct);
        await db.WorkflowStages.ExecuteDeleteAsync(ct);
        await db.Workflows.ExecuteDeleteAsync(ct);
        await db.ProjectTemplates.ExecuteDeleteAsync(ct);
        await db.OrganizationSettings.ExecuteDeleteAsync(ct);
        await db.Users.ExecuteDeleteAsync(ct);

        // Los registros pendientes no llevan OrganizationId —todavía no hay organización cuando se
        // crean— así que se limpian por su cuenta.
        await db.PendingRegistrations
            .Where(p => p.OrganizationName == organization.Name)
            .ExecuteDeleteAsync(ct);

        // Y por último la organización. Si quedó una sola fila colgando en cualquier tabla, este
        // borrado falla por la clave foránea y se deshace todo.
        await db.Organizations
            .Where(o => o.Id == organizationId)
            .ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
        return archivos;
    }

    /// <summary>Un slug libre a partir del propuesto. Si «acme» está tomado prueba «acme-2»: es
    /// preferible a rechazar el alta por un nombre repetido, que no es culpa de quien se
    /// registra.</summary>
    private async Task<string> FreeSlugAsync(string proposed, CancellationToken ct)
    {
        var taken = await db.Organizations
            .Where(o => o.Slug == proposed || o.Slug.StartsWith(proposed + "-"))
            .Select(o => o.Slug)
            .ToListAsync(ct);

        if (!taken.Contains(proposed)) return proposed;

        for (var n = 2; ; n++)
        {
            var candidate = $"{proposed}-{n}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    /// <summary>Nombre legible a identificador de URL. Sin acentos ni espacios, porque el slug
    /// termina en enlaces y en la línea de comandos.</summary>
    public static string Slugify(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var slug = new string(normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--")) slug = slug.Replace("--", "-");

        // Un nombre escrito solo con símbolos dejaría el slug vacío, y el índice único convertiría
        // a la segunda organización así en un error al guardar.
        if (string.IsNullOrEmpty(slug)) slug = $"org-{Guid.NewGuid().ToString()[..8]}";

        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }
}
