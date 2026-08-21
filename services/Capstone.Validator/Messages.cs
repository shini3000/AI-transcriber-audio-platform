namespace Capstone.Validator;

public record AudioUploadedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);

public record AudioValidatedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);
