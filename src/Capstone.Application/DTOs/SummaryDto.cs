namespace Capstone.Application.DTOs;

public record SummaryDto(
    string SummaryText,
    string ModelName,
    DateTime CreatedAt
);
