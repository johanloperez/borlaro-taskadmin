namespace TaskAdmin.Api.Auth;

/// <summary>Un único proveedor OIDC, configurado desde la pantalla de Configuración. Genérico a
/// propósito: Google es un issuer más, igual que Microsoft Entra, Keycloak, Authentik o Zitadel.
/// Escribir «Google» en el código costaría lo mismo y obligaría a volver a tocarlo el día que
/// alguien use Microsoft 365.</summary>
public class OidcOptions
{
    public const string SectionName = "Oidc";

    public bool Enabled { get; set; }

    /// <summary>URL base del proveedor. El descubrimiento se arma agregándole
    /// `/.well-known/openid-configuration`.</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Solo lo usa el servidor, para canjear el código. Nunca viaja al navegador.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public string ButtonLabel { get; set; } = "Entrar con Google";

    /// <summary>`email` no es opcional: es lo que permite encontrar a quién vincular la cuenta la
    /// primera vez.</summary>
    public string Scopes { get; set; } = "openid email profile";

    /// <summary>Dominios de email permitidos, separados por coma. Vacío = cualquiera.</summary>
    public string AllowedDomains { get; set; } = string.Empty;

    /// <summary>Configurado del todo. Con la mitad de los campos cargados el botón llevaría a un
    /// error del proveedor, así que en ese estado no se muestra.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    public bool IsUsable => Enabled && IsComplete;

    public IReadOnlyList<string> Domains =>
        AllowedDomains
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.TrimStart('@').ToLowerInvariant())
            .ToList();
}
