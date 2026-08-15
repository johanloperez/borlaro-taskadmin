using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;
using TaskAdmin.Infrastructure.Storage;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Api.Endpoints;

public record OrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    string? LogoPath,
    bool IsSuspended,
    DateTimeOffset? SuspendedAt,
    string? SuspendedReason,
    DateTimeOffset CreatedAt,
    int People,
    int Projects,
    int OpenItems,
    DateTimeOffset? LastActivityAt,
    OrganizationUsage Usage);

/// <summary>Cuánto consumió del modelo esta organización en los últimos 30 días.
///
/// Existe porque con la clave de la plataforma pagando por defecto, el costo del agente no tiene
/// dueño: se sabe cuánto se gastó en total y no de quién fue. Sin este número, el primer mes de
/// factura alta es una investigación; con él, es una fila.
///
/// Cuenta tokens y no dinero a propósito: el precio depende del modelo, del proveedor y del día,
/// y una cifra en pesos calculada acá envejecería mal y en silencio.</summary>
public record OrganizationUsage(
    int CheckIns,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    /// <summary>Si la organización paga su propio modelo. Cuando es true, estos tokens no salen
    /// de la clave de la plataforma.</summary>
    bool OwnModel);

public record SuspendBody(string? Reason);

/// <summary>La consola de quien opera la instalación: qué organizaciones hay, cómo van de uso, y
/// el interruptor para suspenderlas y reactivarlas.
///
/// Lo que **no** hay acá es una forma de mirar adentro. El operador ve que la organización existe,
/// cuánta gente tiene y cuándo se movió por última vez; no ve un proyecto, ni una tarea, ni una
/// conversación con el agente. Un botón de «ver todo» sin traza es lo primero que objeta cualquier
/// empresa que evalúe alojar su trabajo acá, y una vez que existe ya no se puede prometer lo
/// contrario. Si algún día hace falta para soporte, va a ser un acceso pedido, consentido por el
/// administrador de esa organización, con vencimiento y registrado.</summary>
public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        var platform = app.MapGroup("/api/platform")
            .WithTags("Plataforma")
            .RequireAuthorization(Policies.IsPlatformOperator);

        platform.MapGet("/organizations", async (
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            // Sin filtro, que es justamente lo que distingue a esta consola: es el único lugar de
            // la API que mira por encima de las organizaciones, y solo cuenta filas.
            using var scope = OrganizationScope.UseSystem();

            var since = DateTimeOffset.UtcNow.AddDays(-30);

            var rows = await db.Organizations
                .AsNoTracking()
                .Where(o => !o.IsPlatform)
                .OrderBy(o => o.CreatedAt)
                .Select(o => new OrganizationSummary(
                    o.Id,
                    o.Name,
                    o.Slug,
                    o.LogoPath,
                    o.IsSuspended,
                    o.SuspendedAt,
                    o.SuspendedReason,
                    o.CreatedAt,
                    db.Users.Count(u => u.OrganizationId == o.Id && u.IsActive),
                    db.Projects.Count(p => p.OrganizationId == o.Id && !p.IsArchived),
                    db.WorkItems.Count(i => i.OrganizationId == o.Id && i.ClosedAt == null),
                    db.WorkItemEvents
                        .Where(e => e.OrganizationId == o.Id)
                        .Max(e => (DateTimeOffset?)e.CreatedAt),
                    new OrganizationUsage(
                        db.CheckIns.Count(c => c.OrganizationId == o.Id && c.CreatedAt >= since),
                        db.CheckIns.Where(c => c.OrganizationId == o.Id && c.CreatedAt >= since)
                            .Sum(c => (long)c.InputTokens),
                        db.CheckIns.Where(c => c.OrganizationId == o.Id && c.CreatedAt >= since)
                            .Sum(c => (long)c.OutputTokens),
                        db.CheckIns.Where(c => c.OrganizationId == o.Id && c.CreatedAt >= since)
                            .Sum(c => (long)c.CacheReadTokens),

                        // De qué bolsillo salen esos tokens. Cuenta tanto la clave propia —paga
                        // ella— como una URL base propia —su Ollama, que no le cuesta a nadie—.
                        // Se mira que la fila exista, nunca su valor.
                        db.OrganizationSettings.Any(s =>
                            s.OrganizationId == o.Id &&
                            (s.Key == "AgentModel:ApiKey" || s.Key == "AgentModel:BaseUrl")))))
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("ListOrganizations");

        platform.MapPost("/organizations", async (
            HttpRequest request,
            TaskAdminDbContext db,
            OrganizationService organizations,
            IFileStore files,
            CancellationToken ct) =>
        {
            // Multipart a propósito: el logo viaja en el mismo pedido que el alta, así no queda
            // ningún archivo huérfano si la creación falla — el que se haya guardado se borra.
            if (!request.HasFormContentType)
            {
                return Results.Problem("Se espera multipart/form-data.", statusCode: 400);
            }

            var form = await request.ReadFormAsync(ct);

            var name = form["name"].FirstOrDefault()?.Trim();
            var adminEmail = form["adminEmail"].FirstOrDefault()?.Trim().ToLowerInvariant();
            var adminPassword = form["adminPassword"].FirstOrDefault();
            var slug = form["slug"].FirstOrDefault();
            var adminName = form["adminName"].FirstOrDefault();
            var timeZoneId = form["timeZoneId"].FirstOrDefault();
            var logo = form.Files.GetFile("logo");

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(adminEmail) ||
                string.IsNullOrWhiteSpace(adminPassword))
            {
                return Results.Problem("Faltan campos obligatorios: nombre, email o contraseña del administrador.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (adminPassword.Length < 10)
            {
                return Results.Problem("La contraseña debe tener al menos 10 caracteres.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string? logoPath = null;
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
                var (organization, admin) = await organizations.CreateAsync(
                    name,
                    adminEmail,
                    adminName ?? string.Empty,
                    BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    slug,
                    timeZoneId ?? "UTC",
                    logoPath,
                    ct);

                return Results.Created(
                    $"/api/platform/organizations/{organization.Id}",
                    new { organization.Id, organization.Name, organization.Slug, admin = admin.Email });
            }
            catch (DomainException ex)
            {
                if (logoPath is not null) await files.DeleteAsync(logoPath, ct);
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateOrganization");

        platform.MapPost("/organizations/{id:guid}/suspender", async (
            Guid id,
            SuspendBody body,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            using var scope = OrganizationScope.UseSystem();

            var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsPlatform, ct);
            if (organization is null) return Results.NotFound();

            organization.IsSuspended = true;
            organization.SuspendedAt = DateTimeOffset.UtcNow;
            organization.SuspendedReason = body.Reason?.Trim();

            await db.SaveChangesAsync(ct);

            // Suspender no borra nada: la gente deja de poder entrar y el trabajo queda intacto.
            // Un borrado sería irreversible, y casi nunca es lo que se quiere cuando alguien deja
            // de pagar o hay una denuncia por revisar.
            return Results.Ok(new { organization.Id, organization.IsSuspended });
        })
        .WithName("SuspendOrganization");

        platform.MapPost("/organizations/{id:guid}/reactivar", async (
            Guid id,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            using var scope = OrganizationScope.UseSystem();

            var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsPlatform, ct);
            if (organization is null) return Results.NotFound();

            organization.IsSuspended = false;
            organization.SuspendedAt = null;
            organization.SuspendedReason = null;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { organization.Id, organization.IsSuspended });
        })
        .WithName("ReactivateOrganization");

        /// Borrar una organización y todo lo suyo. No hay vuelta atrás y no hay papelera.
        ///
        /// Se confirma escribiendo el nombre exacto. No es teatro: es lo único que distingue
        /// «quiero borrar *esta*» de «hice clic donde no era», y en una lista de organizaciones
        /// parecidas es un error fácil de cometer.
        platform.MapDelete("/organizations/{id:guid}", async (
            Guid id,
            string? confirmar,
            OrganizationService organizations,
            TaskAdminDbContext db,
            IFileStore files,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            using var scope = OrganizationScope.UseSystem();

            var organization = await db.Organizations.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (organization is null) return Results.NotFound();

            if (!string.Equals(confirmar?.Trim(), organization.Name, StringComparison.Ordinal))
            {
                return Results.Problem(
                    $"Para borrarla hay que escribir su nombre exacto: «{organization.Name}».",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var logger = loggers.CreateLogger("Plataforma");

            IReadOnlyList<string> archivos;
            try
            {
                archivos = await organizations.DeleteAsync(id, ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            // Queda registrado en el log del servidor porque en la base no queda nada: borrada la
            // organización, se fue también cualquier rastro que pudiéramos haberle escrito adentro.
            logger.LogWarning(
                "Organización «{Nombre}» ({Id}) borrada por completo: {Archivos} archivos a limpiar",
                organization.Name, id, archivos.Count);

            // Los archivos, recién ahora: si la transacción hubiera fallado, siguen estando.
            var limpiados = 0;
            foreach (var path in archivos)
            {
                try
                {
                    await files.DeleteAsync(path, ct);
                    limpiados++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Un archivo que no se pudo borrar queda huérfano y ocupa disco, pero la
                    // organización ya no existe: no hay nada que deshacer ni motivo para fallar.
                    logger.LogWarning(ex, "No se pudo borrar {Ruta} de la organización borrada", path);
                }
            }

            return Results.Ok(new { borrada = organization.Name, archivos = limpiados });
        })
        .WithName("DeleteOrganization");

        // ── Público: en qué modo corre la instalación ────────────────────────────
        // Lo consulta la pantalla de entrada para saber si mostrar «Creá tu organización». Dice
        // el modo y nada más: ni cuántas organizaciones hay, ni cuáles.
        app.MapGet("/api/tenancy", (IOptionsMonitor<TenancyOptions> tenancy) =>
        {
            var options = tenancy.CurrentValue;
            return Results.Ok(new
            {
                mode = options.Mode.ToString(),
                multi = options.IsMulti,
                registrationOpen = options.RegistrationOpen
            });
        })
        .WithTags("Plataforma")
        .AllowAnonymous()
        .WithName("TenancyMode");

        // El logo se sirve acá, con sesión, y no como archivo público: las organizaciones no se
        // ven entre sí, y su logo tampoco. El `<img>` no puede mandar el header de autorización,
        // así que el frontend lo trae con fetch y lo convierte en una URL de blob.
        app.MapGet("/api/organizations/{id:guid}/logo", async (
            Guid id,
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            IFileStore files,
            CancellationToken ct) =>
        {
            // Solo la propia, o cualquiera si sos operador —que es quien las lista en su consola—.
            //
            // Hace falta comprobarlo a mano: `Organization` no implementa `IOrganizationScoped`
            // —es la organización, no algo que le pertenezca— así que el filtro global que separa
            // a las empresas no cubre esta tabla. Con solo `RequireAuthorization`, cualquier
            // persona con sesión podía pedir el logo de otra empresa sabiendo su id, y la
            // diferencia entre 200 y 404 ya confirmaba que ese id existe.
            var esOperador = principal.IsInRole(nameof(UserRole.PlatformOperator));
            if (!esOperador && principal.OrganizationId() != id) return Results.NotFound();

            var organization = await db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (organization?.LogoPath is null) return Results.NotFound();

            var stream = await files.OpenAsync(organization.LogoPath, ct);
            return Results.File(stream, LogoUpload.ContentTypeFor(organization.LogoPath));
        })
        .WithTags("Plataforma")
        .RequireAuthorization()
        .WithName("OrganizationLogo");

        return app;
    }
}
