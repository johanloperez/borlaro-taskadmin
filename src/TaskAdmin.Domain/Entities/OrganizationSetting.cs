namespace TaskAdmin.Domain.Entities;

/// <summary>Un ajuste que una organización decidió no heredar.
///
/// El modelo es de dos capas y en ese orden: la plataforma pone un valor —su clave de Anthropic,
/// su SMTP— y la organización puede reemplazarlo. Sin fila acá, hereda; con fila, manda la suya.
/// Es lo que permite que una empresa nueva funcione el primer día sin configurar nada y que otra
/// apunte el agente a su Ollama local sin afectar a nadie más.
///
/// Vive en su propia tabla y no en <see cref="AppSetting"/> porque esa se carga como fuente de
/// `IConfiguration` del proceso: un diccionario único para toda la app, leído una vez al arrancar.
/// Un override por empresa ahí sería el override de todas.</summary>
public class OrganizationSetting : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>Misma notación que la configuración de .NET —«AgentModel:Model»— y las mismas
    /// claves del catálogo, para que heredar sea comparar la misma cadena.</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>Guardado cifrado, y nunca devuelto por la API. La clave del modelo de una empresa
    /// no la puede leer ni el operador de la instalación.</summary>
    public bool IsSecret { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }
}
