using Capstone.Application.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Capstone.Infrastructure.FileStorage;

public class MinIOStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly MinIOSettings _settings;

    public MinIOStorageService(IOptions<MinIOSettings> settings)
    {
        _settings = settings.Value;

        _minioClient = new MinioClient()
            .WithEndpoint(_settings.Endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithSSL(_settings.UseSsl)
            .Build();
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var objectName = $"{Guid.NewGuid()}_{fileName}";

        // Ensure the bucket exists
        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_settings.BucketName),
            cancellationToken);

        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_settings.BucketName),
                cancellationToken);
        }

        // Upload the file
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

        // Build the public URL
        var protocol = _settings.UseSsl ? "https" : "http";
        return $"{protocol}://{_settings.Endpoint}/{_settings.BucketName}/{objectName}";
    }
}
