namespace Capstone.Domain.Entities;

public class AudioTranscription
{
    public Guid Id { get; set; }
    public Guid AudioFileId { get; set; }
    public string FullText { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }

    public AudioFile AudioFile { get; set; } = null!;
    public AudioSummary? Summary { get; set; }

    public static AudioTranscription Create(
        Guid audioFileId,
        string fullText,
        string modelName,
        long processingTimeMs)
    {
        return new AudioTranscription
        {
            Id = Guid.NewGuid(),
            AudioFileId = audioFileId,
            FullText = fullText,
            ModelName = modelName,
            ProcessingTimeMs = processingTimeMs,
            CreatedAt = DateTime.UtcNow
        };
    }
}
