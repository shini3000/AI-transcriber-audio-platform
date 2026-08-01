namespace Capstone.Application.DTOs;

public record TranscriptionDto(
    string FullText,
    string ModelName,
    long ProcessingTimeMs,
    DateTime CreatedAt
);
