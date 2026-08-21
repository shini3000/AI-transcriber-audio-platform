using Capstone.Validator;
using Capstone.Validator.Messaging;
using Capstone.Validator.Services;
using Capstone.Validator.Workers;
using Microsoft.EntityFrameworkCore;
using Minio;

var currentDir = Directory.GetCurrentDirectory();
string? envPath = null;
while (currentDir != null)
{
    var candidate = Path.Combine(currentDir, ".env");
    if (File.Exists(candidate))
    {
        envPath = candidate;
        break;
    }
    currentDir = Directory.GetParent(currentDir)?.FullName;
}

if (envPath != null)
{
    DotNetEnv.Env.Load(envPath);
    Console.WriteLine($"[Config] Archivo .env cargado desde: {envPath}");
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IMinioClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new MinioClient()
        .WithEndpoint(configuration["MinIO:Endpoint"] ?? "localhost:9000")
        .WithCredentials(
            configuration["MinIO:AccessKey"] ?? "minioadmin",
            configuration["MinIO:SecretKey"] ?? "minioadmin")
        .WithSSL(bool.Parse(configuration["MinIO:UseSsl"] ?? "false"))
        .Build();
});

builder.Services.AddScoped<AudioFileDownloader>();
builder.Services.AddScoped<AudioMagicBytesValidator>();
builder.Services.AddSingleton<AudioValidatedPublisher>();
builder.Services.AddHostedService<AudioValidationWorker>();

var host = builder.Build();
host.Run();
