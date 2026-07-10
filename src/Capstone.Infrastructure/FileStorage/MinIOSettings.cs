namespace Capstone.Infrastructure.FileStorage;

public class MinIOSettings
{
    public const string SectionName = "MinIO";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "audio";
    public bool UseSsl { get; set; } = false;
}
