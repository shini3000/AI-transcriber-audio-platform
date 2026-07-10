namespace Capstone.Application.DTOs;

public record UploadAudioResponse(
    Guid Id,
    string FileName,
    string StorageUrl,
    DateTime UploadedAt
);
