namespace Capstone.Domain.Entities;

public class AudioFile
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    public static AudioFile Create(string fileName, string contentType, long size, string storageUrl)
    {
        return new AudioFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            StorageUrl = storageUrl,
            UploadedAt = DateTime.UtcNow
        };
    }
}
