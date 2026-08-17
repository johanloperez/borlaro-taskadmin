using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Infrastructure.Notifications;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Infrastructure.Settings;

/// <summary>El valor efectivo de un ajuste para la organización en la que se está trabajando: el
/// suyo si lo definió, y si no el de la plataforma.
///
/// Las dos capas existen para que una empresa nueva funcione el primer día sin configurar nada
/// —el agente conversa, los emails salen— y para que la que quiera cambiarlo pueda, sin pedirle
/// permiso a nadie ni afectar al resto. Es la misma decisión que toma cualquier producto alojado
/// con opción de on-premise: traer pilas incluidas y no atarlas.</summary>
public class OrganizationSettings(BorlaroTmsDbContext db, IConfiguration configuration, SecretProtector protector)
{
    /// <summary>Los overrides de la organización activa, ya descifrados. Se lee una vez por uso y
    /// no clave por clave: son pocas filas y el modelo necesita media docena de ellas junta.</summary>
    private async Task<Dictionary<string, string>> OverridesAsync(CancellationToken ct)
    {
        // Sin organización activa —un proceso de fondo que todavía no entró en ninguna— no hay
        // override posible: se hereda todo.
        if (OrganizationScope.Organization is null) return [];

        var rows = await db.OrganizationSettings.AsNoTracking().ToListAsync(ct);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var value = row.IsSecret ? protector.Unprotect(row.Value) : row.Value;
            if (!string.IsNullOrWhiteSpace(value)) result[row.Key] = value;
        }

        return result;
    }

    public async Task<string?> ValueAsync(string key, CancellationToken ct = default)
    {
        var overrides = await OverridesAsync(ct);
        return overrides.TryGetValue(key, out var value) ? value : configuration[key];
    }

    /// <summary>Con qué modelo habla el agente en esta organización.</summary>
    public async Task<AgentModelOptions> AgentModelAsync(CancellationToken ct = default)
    {
        var overrides = await OverridesAsync(ct);

        string? Read(string key) =>
            overrides.TryGetValue(key, out var value) ? value : configuration[key];

        var options = new AgentModelOptions();
        configuration.GetSection(AgentModelOptions.SectionName).Bind(options);

        // Se parte de la configuración de la plataforma y se pisa lo que la organización haya
        // decidido: así un override parcial —solo el modelo, manteniendo la clave de la
        // plataforma— es exactamente eso y no una configuración a medio armar.
        if (Read("AgentModel:Provider") is { } provider) options.Provider = provider;
        if (Read("AgentModel:Model") is { } model) options.Model = model;
        if (Read("AgentModel:ApiKey") is { } key) options.ApiKey = key;
        if (Read("AgentModel:BaseUrl") is { } baseUrl) options.BaseUrl = baseUrl;
        if (int.TryParse(Read("AgentModel:MaxTurns"), out var turns)) options.MaxTurns = turns;
        if (int.TryParse(Read("AgentModel:MaxTokens"), out var tokens)) options.MaxTokens = tokens;
        if (Read("AgentModel:Effort") is { } effort) options.Effort = effort;

        return options;
    }

    /// <summary>Por qué SMTP salen los avisos de esta organización.</summary>
    public async Task<EmailOptions> EmailAsync(CancellationToken ct = default)
    {
        var overrides = await OverridesAsync(ct);

        string? Read(string key) =>
            overrides.TryGetValue(key, out var value) ? value : configuration[key];

        var options = new EmailOptions();
        configuration.GetSection(EmailOptions.SectionName).Bind(options);

        // Cambiar de servidor SMTP es todo o nada: si la organización definió su host, las
        // credenciales tienen que ser las suyas y no las que quedaron de la plataforma —un
        // usuario y contraseña de otro servidor no autentican contra este, y el error aparecería
        // recién en el primer email que no llega.
        if (overrides.ContainsKey("Email:SmtpHost"))
        {
            options.SmtpHost = Read("Email:SmtpHost");
            options.Username = overrides.GetValueOrDefault("Email:Username");
            options.Password = overrides.GetValueOrDefault("Email:Password");
            if (int.TryParse(Read("Email:SmtpPort"), out var port)) options.SmtpPort = port;
            if (bool.TryParse(Read("Email:UseStartTls"), out var tls)) options.UseStartTls = tls;
        }

        if (Read("Email:FromAddress") is { } from) options.FromAddress = from;
        if (Read("Email:FromName") is { } fromName) options.FromName = fromName;

        return options;
    }
}
