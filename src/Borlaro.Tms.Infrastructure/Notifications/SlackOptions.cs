namespace Borlaro.Tms.Infrastructure.Notifications;

/// <summary>Las credenciales de la app de Slack de una organización.
///
/// Van por organización y no por instalación porque cada empresa tiene su propio workspace: la
/// que aloja a dos clientes necesita dos apps de Slack distintas, y una sola configuración global
/// haría que los check-ins de una empresa salieran por el bot de la otra.</summary>
public class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>Token del bot (`xoxb-…`). Vacío = Slack apagado para esta organización, y el
    /// canal simplemente responde que no puede entregar: la escalera lo saltea sola.</summary>
    public string? BotToken { get; set; }

    /// <summary>El secreto con el que Slack firma cada webhook. Sin esto no se puede distinguir
    /// un evento de Slack de uno que mandó cualquiera a la URL pública, así que sin secreto
    /// configurado **no se acepta ningún evento**.</summary>
    public string? SigningSecret { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);
}
