namespace Borlaro.Tms.Domain.Entities;

/// <summary>Un ajuste editable desde la interfaz. La clave usa la misma notación que la
/// configuración de .NET —«Email:SmtpHost», «Notifications:FirstEmailAfterMinutes»— para que un
/// valor guardado acá pise exactamente al de `appsettings.json` sin tablas de traducción.
///
/// Existe porque la alternativa es que cambiar el horario de un check-in requiera acceso al
/// servidor, y eso convierte cada ajuste en un ticket.</summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Guardado cifrado cuando <see cref="IsSecret"/> es true.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Claves y contraseñas. Nunca se devuelven por la API: la interfaz muestra si
    /// están definidas, no su valor.</summary>
    public bool IsSecret { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }
}
