namespace Capstone.Domain.Entities;

public class AudioSummary
{
    public Guid Id { get; set; }
    public Guid AudioFileId { get; set; }
    public Guid TranscriptionId { get; set; }
    public string SummaryText { get; set; } = string.Empty;  
    public string ModelName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public AudioFile AudioFile { get; set; } = null!;
    public AudioTranscription Transcription { get; set; } = null!;

    public static AudioSummary Create(
        Guid audioFileId,
        Guid transcriptionId,
        string summaryText,
        string modelName)
    {
        return new AudioSummary
        {
            Id = Guid.NewGuid(),
            AudioFileId = audioFileId,
            TranscriptionId = transcriptionId,
            SummaryText = summaryText,
            ModelName = modelName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
