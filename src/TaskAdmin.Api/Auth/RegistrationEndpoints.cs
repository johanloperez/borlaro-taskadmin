using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskAdmin.Api.Localization;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Notifications;
using TaskAdmin.Infrastructure.Services;
using TaskAdmin.Infrastructure.Storage;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Api.Auth;

public record ConfirmSignUpBody([Required] string Token);

/// <summary>Abrir la cuenta de una empresa con email y contraseña.
///
/// El camino largo, y a propósito: la organización no se crea al enviar el formulario sino al
/// hacer clic en el enlace del correo. Sin esa vuelta, un endpoint público que crea empresas es
/// una invitación a llenar la base de organizaciones a nombre de direcciones inventadas, y
/// limpiarlas después es entrar a la base a mano.
///
/// Con el proveedor de identidad no hace falta ninguna de estas dos pantallas: Google ya verificó
/// la dirección, así que ese camino entra derecho. Los dos conviven porque no todo el mundo tiene
/// —ni quiere usar— una cuenta de Google para trabajar.</summary>
public static class RegistrationEndpoints
{
    /// <summary>Límite de tasa del alta. Es la segunda ruta anónima que escribe en la base, y la
    /// que además manda correo: sin tope, sirve para inundar de emails a terceros.</summary>
    public const string RateLimitPolicy = "registro";

    public const int MinPasswordLength = 10;

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/registro")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/", async (
            HttpRequest request,
            HttpContext http,
            TaskAdminDbContext db,
            IOptionsMonitor<TenancyOptions> tenancyOptions,
            EmailChannel email,
            IFileStore files,
            CancellationToken ct) =>
        {
            // Multipart: el logo viaja en el mismo pedido que el alta.
            if (!request.HasFormContentType)
            {
                return Results.Problem("Se espera multipart/form-data.", statusCode: 400);
            }

            var form = await request.ReadFormAsync(ct);

            var organizationName = form["organizationName"].FirstOrDefault();
            var name = form["name"].FirstOrDefault();
            var password = form["password"].FirstOrDefault();
            var logo = form.Files.GetFile("logo");

            // Todavía no hay sesión, así que el idioma sale del `Accept-Language` que manda el
            // frontend con el que la persona eligió en la pantalla.
            var lang = http.Language();
            var tenancy = tenancyOptions.CurrentValue;
            if (!tenancy.RegistrationOpen) return Results.NotFound();

            var address = form["email"].FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (!address.Contains('@') || address.Length > 320)
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.SignUpInvalidEmail),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!tenancy.DomainAllowed(address))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.SignUpDomainNotAllowed),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (password is null || password.Length < MinPasswordLength)
            {
                return Results.Problem(
                    Messages.Get(lang, Messages.Key.SignUpShortPassword, ("min", MinPasswordLength)),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(organizationName))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.SignUpNeedsOrganizationName),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // El logo se guarda antes de mirar si la dirección ya tiene cuenta, y se borra si al
            // final no queda ninguna alta pendiente con él: un archivo que no cuelga de nada es
            // solo basura ocupando el disco.
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
                using (OrganizationScope.UseSystem())
                {
                    // Barrido de las vencidas acá mismo: la tabla crece de a una fila por alta y no
                    // justifica un job aparte.
                    var stale = DateTimeOffset.UtcNow - PendingRegistration.Lifetime;
                    await db.PendingRegistrations
                        .Where(p => p.CreatedAt < stale || p.ConfirmedAt != null)
                        .ExecuteDeleteAsync(ct);

                    var taken = await db.Users.AnyAsync(u => u.Email == address, ct);

                    // Si la dirección ya tiene cuenta, la respuesta es la misma que si no: «te
                    // mandamos un correo». Decir «ese email ya existe» convierte al formulario en un
                    // detector de quién usa la herramienta. Lo que cambia es el correo que sale.
                    if (taken)
                    {
                        if (logoPath is not null) await files.DeleteAsync(logoPath, ct);
                        logoPath = null;

                        await email.SendToAsync(
                            name?.Trim() ?? string.Empty,
                            address,
                            Messages.Get(lang, Messages.Key.AlreadyRegisteredSubject),
                            Messages.Get(lang, Messages.Key.AlreadyRegisteredBody),
                            "/login",
                            ct);

                        return Results.Accepted();
                    }

                    // Registrarse dos veces no acumula altas pendientes: la anterior se reemplaza, y
                    // el enlace viejo deja de servir. El logo de la anterior se borra: quedó
                    // huérfano.
                    var previous = await db.PendingRegistrations
                        .Where(p => p.Email == address)
                        .ToListAsync(ct);
                    foreach (var old in previous)
                    {
                        if (old.LogoPath is not null) await files.DeleteAsync(old.LogoPath, ct);
                    }
                    await db.PendingRegistrations.Where(p => p.Email == address).ExecuteDeleteAsync(ct);

                    var pending = new PendingRegistration
                    {
                        Email = address,
                        Name = name?.Trim() ?? string.Empty,
                        OrganizationName = organizationName.Trim(),
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password!),
                        LogoPath = logoPath,
                        Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24))
                    };

                    db.PendingRegistrations.Add(pending);
                    await db.SaveChangesAsync(ct);

                    await email.SendToAsync(
                        pending.Name,
                        pending.Email,
                        Messages.Get(lang, Messages.Key.VerifySubject),
                        Messages.Get(lang, Messages.Key.VerifyBody,
                            ("name", pending.Name), ("organization", pending.OrganizationName)),
                        $"/registro/confirmar?token={pending.Token}",
                        ct);
                }

                // 202 y no 201: todavía no existe nada. Lo único que pasó es que salió un correo.
                return Results.Accepted();
            }
            catch (Exception)
            {
                if (logoPath is not null) await files.DeleteAsync(logoPath, ct);
                throw;
            }
        })
        .WithName("SignUp");

        group.MapPost("/confirmar", async (
            ConfirmSignUpBody body,
            HttpContext http,
            TaskAdminDbContext db,
            IOptionsMonitor<TenancyOptions> tenancyOptions,
            OrganizationService organizations,
            TokenService tokens,
            IFileStore files,
            CancellationToken ct) =>
        {
            if (!tenancyOptions.CurrentValue.RegistrationOpen) return Results.NotFound();

            var lang = http.Language();

            using var scope = OrganizationScope.UseSystem();

            var pending = await db.PendingRegistrations.FirstOrDefaultAsync(p => p.Token == body.Token, ct);

            if (pending is null || !pending.IsUsable(DateTimeOffset.UtcNow))
            {
                return Results.Problem(Messages.Get(lang, Messages.Key.SignUpLinkExpired),
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Entre el registro y el clic pueden pasar horas, y en el medio un admin pudo dar de
            // alta esta dirección a mano. El logo del alta vencida se borra: quedó huérfano.
            if (await db.Users.AnyAsync(u => u.Email == pending.Email, ct))
            {
                if (pending.LogoPath is not null) await files.DeleteAsync(pending.LogoPath, ct);
                db.PendingRegistrations.Remove(pending);
                await db.SaveChangesAsync(ct);

                return Results.Problem(Messages.Get(lang, Messages.Key.SignUpEmailTaken),
                    statusCode: StatusCodes.Status409Conflict);
            }

            try
            {
                var (_, owner) = await organizations.CreateAsync(
                    pending.OrganizationName,
                    pending.Email,
                    pending.Name,
                    pending.PasswordHash,
                    logoPath: pending.LogoPath,
                    ct: ct);

                pending.ConfirmedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                // Se entra directo: pedirle que escriba la contraseña que acaba de elegir, en la
                // pantalla siguiente, es un paso que no protege nada.
                var (token, expiresAt) = tokens.Issue(owner);
                return Results.Ok(new AuthResponse(token, expiresAt, await AuthEndpoints.ToResponseAsync(db, owner, ct)));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("ConfirmSignUp");

        return app;
    }
}
