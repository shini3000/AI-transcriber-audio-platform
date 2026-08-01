namespace Capstone.Domain.Entities;

public class AudioFile
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalContentType { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public DateTime? CompressedAt { get; set; }
    public long CompressionDurationMs { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public static AudioFile Create(
        string fileName,
        string contentType,
        long size,
        string storageUrl,
        string? originalFileName = null,
        string? originalContentType = null,
        long? originalSize = null)
    {
        return new AudioFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            StorageUrl = storageUrl,
            UploadedAt = DateTime.UtcNow,
            OriginalFileName = originalFileName ?? fileName,
            OriginalContentType = originalContentType ?? contentType,
            OriginalSize = originalSize ?? size,
            CompressedAt = originalSize.HasValue ? DateTime.UtcNow : null
        };
    }
}
