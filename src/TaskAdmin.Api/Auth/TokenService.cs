using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskAdmin.Domain.Entities;

namespace TaskAdmin.Api.Auth;

public class TokenService(IOptionsMonitor<JwtOptions> options)
{
    /// <summary>Nombre del claim con la organización de la sesión.</summary>
    public const string OrganizationClaim = "org";

    /// <summary>Idioma elegido por la persona. Viaja en el token para que el servidor pueda
    /// contestarle en su idioma sin ir a buscarlo a la base en cada request.</summary>
    public const string LanguageClaim = "lang";

    // Por monitor: la duración de la sesión se edita desde Configuración. La clave de firma no
    // se relee nunca desde ahí —vive en el entorno— así que este monitor solo mueve los minutos.
    private JwtOptions _options => options.CurrentValue;

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),

            // La organización viaja firmada en el token y no se busca en la base en cada request:
            // es lo que decide qué filas ve la sesión, así que tiene que ser inalterable por el
            // cliente. Un header o un parámetro serían editables desde el navegador.
            new(OrganizationClaim, user.OrganizationId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Language))
        {
            claims.Add(new Claim(LanguageClaim, user.Language));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
