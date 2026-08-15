namespace Capstone.Compressor;

public record AudioUploadedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);

public record AudioCompressedMessage(
    Guid AudioFileId,
    string StorageUrl,
    string FileName
);
