namespace Capstone.Transcriber;

public record AudioUploadedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);
