using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Services;

namespace Borlaro.Tms.Infrastructure.Settings;

public record SettingView(
    string Key,
    string Group,
    string Label,
    string Help,
    SettingKind Kind,
    IReadOnlyList<string>? Options,
    /// <summary>Valor actual. En los secretos viaja siempre null.</summary>
    string? Value,
    /// <summary>Para los secretos: si hay algo cargado, sin decir qué.</summary>
    bool IsSet,
    /// <summary>False cuando el valor viene de appsettings o del entorno y nadie lo editó todavía.</summary>
    bool IsOverridden,
    DateTimeOffset? UpdatedAt);

/// <summary>Lee y escribe los ajustes que el admin puede tocar. Escribir recarga la
/// configuración en caliente: los consumidores usan IOptionsMonitor, así que el cambio aplica
/// sin reiniciar el servidor —que es la diferencia entre una pantalla de configuración y una
/// lista de deseos.</summary>
public class SettingsService(
    BorlaroTmsDbContext db,
    IConfiguration configuration,
    SecretProtector protector)
{
    /// <summary>Los ajustes de un alcance, con su valor efectivo.
    ///
    /// En el alcance de organización «efectivo» quiere decir dos capas: lo que la empresa definió,
    /// o lo que hereda de la plataforma. La diferencia entre las dos importa en pantalla —una cosa
    /// es «el agente usa Claude porque lo decidimos» y otra «porque viene así»— y por eso viaja en
    /// <see cref="SettingView.IsOverridden"/>.</summary>
    public async Task<IReadOnlyList<SettingView>> ReadAllAsync(
        SettingScope scope,
        CancellationToken ct = default)
    {
        var platform = await db.AppSettings.AsNoTracking().ToDictionaryAsync(
            s => s.Key, s => s, StringComparer.OrdinalIgnoreCase, ct);

        var organization = scope == SettingScope.Organization
            ? await db.OrganizationSettings.AsNoTracking().ToDictionaryAsync(
                s => s.Key, s => s, StringComparer.OrdinalIgnoreCase, ct)
            : [];

        return SettingsCatalog.All.Where(d => d.Scope == scope).Select(def =>
        {
            var isSecret = def.Kind == SettingKind.Secret;

            if (scope == SettingScope.Organization)
            {
                organization.TryGetValue(def.Key, out var own);

                var inherited = configuration[def.Key];
                var effective = own is null
                    ? inherited
                    : own.IsSecret ? protector.Unprotect(own.Value) : own.Value;

                return new SettingView(
                    def.Key, def.Group, def.Label, def.Help, def.Kind, def.Options,
                    isSecret ? null : effective,
                    !string.IsNullOrWhiteSpace(effective),
                    own is not null,
                    own?.UpdatedAt);
            }

            platform.TryGetValue(def.Key, out var row);
            var current = configuration[def.Key];

            return new SettingView(
                def.Key, def.Group, def.Label, def.Help, def.Kind, def.Options,
                // Un secreto no vuelve nunca por la API, ni siquiera al admin que lo cargó: la
                // pantalla muestra que está definido, no cuál es.
                isSecret ? null : current,
                !string.IsNullOrWhiteSpace(current),
                row is not null,
                row?.UpdatedAt);
        }).ToList();
    }

    /// <summary>Guarda los ajustes que cambiaron. Un valor vacío borra la fila y devuelve el
    /// ajuste a lo que diga appsettings o el entorno, en vez de dejar un vacío que rompa.</summary>
    public async Task SaveAsync(
        IReadOnlyDictionary<string, string?> values,
        Guid? actorId,
        SettingScope scope,
        CancellationToken ct = default)
    {
        foreach (var (key, raw) in values)
        {
            var def = SettingsCatalog.Find(key)
                ?? throw new DomainException($"«{key}» no es un ajuste configurable.");

            // Que el alcance coincida no es una formalidad: sin esta línea, un administrador
            // podría mandar «Oidc:ClientSecret» al endpoint de su organización y editar el login
            // de toda la instalación.
            if (def.Scope != scope)
            {
                throw new DomainException(def.Scope == SettingScope.Platform
                    ? $"«{def.Label}» lo configura quien opera la instalación, no cada organización."
                    : $"«{def.Label}» se configura dentro de cada organización.");
            }

            var value = raw?.Trim();
            var isSecret = def.Kind == SettingKind.Secret;

            if (string.IsNullOrEmpty(value))
            {
                // Vaciar no deja un hueco: borra la fila. En la plataforma eso devuelve el ajuste
                // a appsettings o al entorno; en una organización, a heredar de la plataforma —que
                // es la forma de decir «volvé a lo de siempre» sin tener que saber cuál era.
                if (scope == SettingScope.Organization)
                {
                    var own = await db.OrganizationSettings.FirstOrDefaultAsync(s => s.Key == def.Key, ct);
                    if (own is not null) db.OrganizationSettings.Remove(own);
                }
                else
                {
                    var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == def.Key, ct);
                    if (existing is not null) db.AppSettings.Remove(existing);
                }

                continue;
            }

            Validate(def, value);

            if (scope == SettingScope.Organization)
            {
                var own = await db.OrganizationSettings.FirstOrDefaultAsync(s => s.Key == def.Key, ct);
                if (own is null)
                {
                    own = new OrganizationSetting { Key = def.Key };
                    db.OrganizationSettings.Add(own);
                }

                own.IsSecret = isSecret;
                own.Value = isSecret ? protector.Protect(value) : value;
                own.UpdatedAt = DateTimeOffset.UtcNow;
                own.UpdatedById = actorId;
                continue;
            }

            var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == def.Key, ct);
            if (row is null)
            {
                row = new AppSetting { Key = def.Key };
                db.AppSettings.Add(row);
            }

            row.IsSecret = isSecret;
            row.Value = isSecret ? protector.Protect(value) : value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedById = actorId;
        }

        await db.SaveChangesAsync(ct);

        // Recargar es lo que hace que el ajuste tenga efecto sin reiniciar. Sin esta línea la
        // pantalla guardaría en una tabla que nadie vuelve a leer hasta el próximo despliegue.
        // Solo hace falta para los de plataforma: los de organización no viven en IConfiguration
        // justamente porque ahí no cabe un valor por empresa.
        if (scope == SettingScope.Platform) (configuration as IConfigurationRoot)?.Reload();
    }

    private static void Validate(SettingDefinition def, string value)
    {
        switch (def.Kind)
        {
            case SettingKind.Number when !long.TryParse(value, out var number) || number < 0:
                throw new DomainException($"«{def.Label}» tiene que ser un número entero positivo.");

            case SettingKind.Boolean when !bool.TryParse(value, out _):
                throw new DomainException($"«{def.Label}» tiene que ser verdadero o falso.");

            case SettingKind.Select when def.Options?.Contains(value) != true:
                throw new DomainException(
                    $"«{def.Label}» solo acepta: {string.Join(", ", def.Options ?? [])}.");

            // La clave de firma se necesita antes de poder leer esta tabla, así que no se guarda
            // acá ni por accidente: sin este corte, un `Jwt:` cualquiera en el catálogo dejaría
            // la instancia sin poder validar sus propios tokens.
            case var _ when def.Key.StartsWith("Jwt:") && def.Key != "Jwt:AccessTokenMinutes":
                throw new DomainException("Las claves de firma no se configuran desde la interfaz.");

            case SettingKind.Text when def.Key == "Defaults:CheckInTime" && !TimeOnly.TryParse(value, out _):
                throw new DomainException("La hora del check-in tiene que tener el formato HH:MM.");

            case SettingKind.Text when def.Key.EndsWith("BaseUrl") &&
                                       !Uri.TryCreate(value, UriKind.Absolute, out _):
                throw new DomainException($"«{def.Label}» tiene que ser una URL completa, con http:// o https://.");

            // El descubrimiento se arma pegándole `/.well-known/openid-configuration` a esto. Una
            // autoridad mal escrita no falla al guardar: falla recién cuando alguien hace clic en
            // el botón y se come un error del proveedor que no explica nada.
            case SettingKind.Text when def.Key == "Oidc:Authority" && !IsValidAuthority(value):
                throw new DomainException(
                    "La autoridad tiene que ser una URL https completa, sin barra final ni ruta de " +
                    "descubrimiento: https://accounts.google.com, no " +
                    "https://accounts.google.com/.well-known/openid-configuration.");
        }
    }

    /// <summary>https salvo en localhost, donde un Keycloak o un Authentik de desarrollo corren
    /// sin certificado y exigir https solo obligaría a inventar uno.</summary>
    private static bool IsValidAuthority(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.AbsolutePath.Contains(".well-known", StringComparison.OrdinalIgnoreCase)) return false;

        var isLoopback = uri.IsLoopback;
        return uri.Scheme == Uri.UriSchemeHttps || (isLoopback && uri.Scheme == Uri.UriSchemeHttp);
    }
}
