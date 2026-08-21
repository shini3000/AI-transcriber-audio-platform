using Minio;
using Minio.DataModel.Args;

namespace Capstone.Compressor.Services;

public class AudioFileDownloader
{
    private readonly ILogger<AudioFileDownloader> _logger;
    private readonly IMinioClient _minioClient;

    public AudioFileDownloader(ILogger<AudioFileDownloader> logger, IMinioClient minioClient)
    {
        _logger = logger;
        _minioClient = minioClient;
    }

    public async Task DownloadFromMinioAsync(string storageUrl, string tempPath)
    {
        var uri = new Uri(storageUrl);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/');
        var bucketName = segments[0];
        var objectName = string.Join("/", segments[1..]);

        _logger.LogDebug("Downloading from MinIO: bucket={Bucket}, object={Object} → {TempPath}",
            bucketName, objectName, tempPath);

        await using var fileStream = File.Create(tempPath);
        await _minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(fileStream)));
    }
}
