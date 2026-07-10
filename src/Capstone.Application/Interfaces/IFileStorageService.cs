namespace Capstone.Application.Interfaces;

/// <summary>
/// Puerto para almacenamiento externo de archivos (implementado por Cloudinary).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Sube un archivo al file store y devuelve la URL pública.
    /// </summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}
