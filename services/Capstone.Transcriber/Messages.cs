namespace Capstone.Transcriber;

public record AudioUploadedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);

public record AudioProcessedMessage(
    Guid AudioFileId
);
