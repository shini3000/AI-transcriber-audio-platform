using Capstone.Application.Interfaces;
using Capstone.Application.UseCases;
using Capstone.Infrastructure.FileStorage;
using Capstone.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    DotNetEnv.Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<MinIOSettings>(
    builder.Configuration.GetSection(MinIOSettings.SectionName));
builder.Services.AddScoped<IFileStorageService, MinIOStorageService>();

builder.Services.AddScoped<IAudioFileRepository, AudioFileRepository>();

builder.Services.AddScoped<UploadAudioUseCase>();
builder.Services.AddControllers();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50_000_000;
});

var app = builder.Build();

app.MapControllers();

app.Run();
