namespace TaskAdmin.Infrastructure.Storage;

public record StoredFile(string Path, long SizeBytes, string ContentType, string OriginalName);

public interface IFileStore
{
    /// <summary>Guarda el contenido y devuelve la ruta relativa con la que recuperarlo.</summary>
    Task<StoredFile> SaveAsync(
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<Stream> OpenAsync(string path, CancellationToken ct = default);

    Task DeleteAsync(string path, CancellationToken ct = default);
}

public class FileStoreOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; set; } = "uploads";

    /// <summary>Tope por archivo. El límite real lo aplica también Kestrel; este es el del dominio.</summary>
    public long MaxBytes { get; set; } = 200 * 1024 * 1024;
}
