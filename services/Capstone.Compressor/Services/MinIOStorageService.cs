using Minio;
using Minio.DataModel.Args;

namespace Capstone.Compressor.Services;

public class MinIOStorageService
{
    private readonly ILogger<MinIOStorageService> _logger;
    private readonly IMinioClient _minioClient;
    private readonly IConfiguration _configuration;

    public MinIOStorageService(
        ILogger<MinIOStorageService> logger,
        IMinioClient minioClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _minioClient = minioClient;
        _configuration = configuration;
    }

    public async Task<string> UploadFileAsync(
        string filePath,
        string fileName,
        string contentType = "audio/aac",
        CancellationToken cancellationToken = default)
    {
        var bucketName = _configuration["MinIO:BucketName"] ?? "audio";
        var endpoint = _configuration["MinIO:Endpoint"] ?? "localhost:9000";
        var useSsl = _configuration.GetValue<bool>("MinIO:UseSsl");
        var objectName = $"{Guid.NewGuid()}_{fileName}";

        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName),
            cancellationToken);

        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName),
                cancellationToken);
        }

        await using var stream = File.OpenRead(filePath);
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

        var protocol = useSsl ? "https" : "http";
        var url = $"{protocol}://{endpoint}/{bucketName}/{objectName}";
        _logger.LogInformation("Uploaded compressed audio to MinIO: {Url}", url);
        return url;
    }
}
