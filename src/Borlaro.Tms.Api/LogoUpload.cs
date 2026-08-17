using Borlaro.Tms.Infrastructure.Storage;

namespace Borlaro.Tms.Api;

/// <summary>Guardar el logo de una organización en el almacenamiento de archivos.
///
/// Es el único lugar donde se valida la subida, y lo comparten los tres caminos que crean una
/// empresa —la consola del operador, el registro con email y el paso tras el proveedor— para que
/// la regla sea una sola: imágenes chicas, y nada más. El nombre que manda el cliente nunca toca
/// el disco, lo maneja <see cref="IFileStore"/>.</summary>
public static class LogoUpload
{
    /// <summary>El logo se dibuja en un encabezado: 2 MB sobra de largo. Un tope chico es además
    /// lo que impide usar este formulario para llenar el disco.</summary>
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];

    /// <summary>Guarda el archivo y devuelve la ruta relativa. Lanza <see cref="InvalidOperationException"/>
    /// con el motivo legible cuando no es una imagen válida o supera el tope.</summary>
    public static async Task<string> SaveAsync(IFileStore files, IFormFile file, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("El logo tiene que ser una imagen: PNG, JPG, GIF, WebP o SVG.");
        }

        if (file.Length > MaxBytes)
        {
            throw new InvalidOperationException($"El logo no puede superar {MaxBytes / 1024} KB.");
        }

        await using var stream = file.OpenReadStream();
        var stored = await files.SaveAsync(stream, file.FileName, file.ContentType, ct);
        return stored.Path;
    }

    /// <summary>El content type con el que servir el logo. Se deduce de la extensión en vez de
    /// guardar una columna aparte: es un archivo que se validó al subir y se sirve tal cual.</summary>
    public static string ContentTypeFor(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
}
