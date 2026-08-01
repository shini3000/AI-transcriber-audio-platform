namespace Capstone.Application.DTOs;

public record AudioResultResponse(
    string Status,
    Guid AudioFileId,
    string? FileName,
    TranscriptionDto? Transcription,
    SummaryDto? Summary
);
