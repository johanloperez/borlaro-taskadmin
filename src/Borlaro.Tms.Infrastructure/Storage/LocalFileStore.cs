using Microsoft.Extensions.Options;

namespace Borlaro.Tms.Infrastructure.Storage;

/// <summary>Almacenamiento en disco. Detrás de IFileStore para poder pasar a S3/R2 cambiando
/// solo el registro en el contenedor.</summary>
public class LocalFileStore : IFileStore
{
    private readonly IOptionsMonitor<FileStoreOptions> _options;
    private readonly string _root;

    // El tope de tamaño se relee en cada uso —es editable desde la interfaz—, pero la carpeta
    // raíz se resuelve una sola vez: moverla en caliente dejaría los archivos ya subidos
    // apuntando a ninguna parte.
    public LocalFileStore(IOptionsMonitor<FileStoreOptions> options)
    {
        _options = options;
        _root = Path.GetFullPath(options.CurrentValue.RootPath);
        Directory.CreateDirectory(_root);
    }

    public long MaxBytes => _options.CurrentValue.MaxBytes;

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        // El nombre que manda el cliente nunca toca el disco: se usa solo como metadato.
        // El archivo se guarda con un nombre generado, así un "..\..\web.config" no puede
        // escribir fuera del directorio ni pisar nada.
        var safeName = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeName);
        if (extension.Length > 20) extension = string.Empty;

        var today = DateTimeOffset.UtcNow;
        var folder = Path.Combine(_root, today.ToString("yyyy"), today.ToString("MM"));
        Directory.CreateDirectory(folder);

        var generated = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, generated);

        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, ct);

            if (file.Length > MaxBytes)
            {
                file.Close();
                File.Delete(fullPath);
                throw new InvalidOperationException(
                    $"El archivo supera el máximo de {MaxBytes / (1024 * 1024)} MB.");
            }
        }

        var info = new FileInfo(fullPath);
        var relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');

        return new StoredFile(relative, info.Length, contentType, safeName);
    }

    public Task<Stream> OpenAsync(string path, CancellationToken ct = default)
    {
        var full = ResolveInsideRoot(path);
        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = ResolveInsideRoot(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>Resuelve la ruta y verifica que siga dentro del directorio raíz. Sin esto, una
    /// ruta guardada en la base con «..» permitiría leer cualquier archivo del servidor.</summary>
    private string ResolveInsideRoot(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));

        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !full.Equals(_root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Ruta fuera del directorio de almacenamiento.");
        }

        return full;
    }
}
