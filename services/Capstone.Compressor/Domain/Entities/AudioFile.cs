namespace Capstone.Compressor.Domain.Entities;

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
}
