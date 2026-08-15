using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskAdmin.Desktop;

/// <summary>Configuración local de la app, guardada en el perfil del usuario.
///
/// Una sola dirección para todo: con Caddy adelante, la web y la API comparten origen, y pedir
/// dos URLs era pedirle a la persona que supiera algo del despliegue. La sesión se obtiene
/// entrando desde la app, no pegando un token emitido en otro lado — eso obligaba a copiar una
/// cadena entre dos pantallas y no había ni dónde pegarla.</summary>
public class AppSettings
{
    public string ServerUrl { get; set; } = "http://localhost";

    /// <summary>Token de sesión, obtenido al entrar desde la app. Sirve para el canal en tiempo
    /// real y para dejar el chat ya autenticado. Vence; se renueva solo con el de dispositivo.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Token del equipo. No vence: es lo que hace que la app no vuelva a pedir la
    /// contraseña. Se revoca desde la web, que es la diferencia con dejar la sesión eterna.</summary>
    public string? DeviceToken { get; set; }

    public string? UserName { get; set; }
    public string? Email { get; set; }

    public bool NotificationsPaused { get; set; }

    /// <summary>Calculada, igual que WebUrl, y por la misma razón marcada: sin esto se escribe en
    /// el JSON un campo que al leer se ignora, y el archivo aparenta guardar un estado que en
    /// realidad se deriva del token.</summary>
    [JsonIgnore]
    public bool HasSession => !string.IsNullOrWhiteSpace(AccessToken);

    /// <summary>La web y la API viven en el mismo origen.
    ///
    /// `JsonIgnore` no es cosmético: esto **fue** un ajuste guardado, y un settings.json viejo
    /// todavía trae su propia `WebUrl`. Al volverse calculada, ese valor guardado dejó de
    /// aplicarse sin que nada lo dijera, y la app quedaba cargando el chat desde el puerto de la
    /// API —un 404 y una ventana en blanco—. Sin esto, `Save()` lo vuelve a escribir y el archivo
    /// sigue pareciendo que configura algo que ya no configura nada.</summary>
    [JsonIgnore]
    public string WebUrl => ServerUrl;

    private static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskAdmin",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Un settings.json corrupto no puede impedir que la app arranque: sin app no hay
            // canal de escritorio, y la persona deja de recibir check-ins.
        }

        return new AppSettings();
    }

    public void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
