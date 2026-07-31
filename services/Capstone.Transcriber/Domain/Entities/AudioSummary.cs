namespace Domain.Entities;

public class AudioSummary
{
    public Guid Id { get; set; }
    public Guid AudioFileId { get; set; }
    public Guid TranscriptionId { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
