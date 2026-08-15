namespace TaskAdmin.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "taskadmin";
    public string Audience { get; set; } = "taskadmin";

    /// <summary>Clave de firma. En producción llega por variable de entorno
    /// (`Jwt__SigningKey`), nunca desde appsettings.json versionado.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60 * 12;
}
