using Capstone.Transcriber.Messaging;
using Capstone.Transcriber.Services;
using Capstone.Transcriber.Workers;
using Microsoft.EntityFrameworkCore;
using Minio;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    DotNetEnv.Env.Load(envPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<TranscriptionModels>();

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

builder.Services.AddSingleton<AudioFileDownloader>();
builder.Services.AddSingleton<AudioTranscriptionService>();
builder.Services.AddSingleton<AudioSummarizationService>();
builder.Services.AddSingleton<AudioProcessedPublisher>();

builder.Services.AddScoped<AudioProcessorService>();

builder.Services.AddHostedService<AudioProcessingWorker>();

var host = builder.Build();
host.Run();
