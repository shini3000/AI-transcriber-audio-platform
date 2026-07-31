using System.Reflection;
using Capstone.Application.Interfaces;
using Capstone.Application.UseCases;
using Capstone.Infrastructure.Compression;
using Capstone.Infrastructure.FileStorage;
using Capstone.Infrastructure.MessageBroker;
using Capstone.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

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
else
{
    Console.WriteLine("[Config] ADVERTENCIA: No se encontró el archivo .env.");
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<MinIOSettings>(
    builder.Configuration.GetSection(MinIOSettings.SectionName));
builder.Services.AddScoped<IFileStorageService, MinIOStorageService>();

builder.Services.Configure<FFmpegCompressionSettings>(
    builder.Configuration.GetSection(FFmpegCompressionSettings.SectionName));
builder.Services.AddScoped<IAudioCompressionService, FFmpegCompressionService>();

var minioSection = builder.Configuration.GetSection("MinIO");
Console.WriteLine($"[Config Debug] MinIO:Endpoint = {minioSection["Endpoint"]}");
Console.WriteLine($"[Config Debug] MinIO:AccessKey = {minioSection["AccessKey"]}");
Console.WriteLine($"[Config Debug] MinIO:SecretKey = {minioSection["SecretKey"]}");
builder.Services.AddScoped<IAudioFileRepository, AudioFileRepository>();
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection(RabbitMQSettings.SectionName));
builder.Services.AddSingleton<IAudioEventPublisher, RabbitMQEventPublisher>();
builder.Services.AddScoped<UploadAudioUseCase>();
builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50_000_000;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Capstone API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

Console.WriteLine("Swagger UI: http://localhost:5069/swagger");

app.Run();
