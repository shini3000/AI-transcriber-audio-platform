namespace Domain.Entities;

public class AudioTranscription
{
    public Guid Id { get; set; }
    public Guid AudioFileId { get; set; }
    public string FullText { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
