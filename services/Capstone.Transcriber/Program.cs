using Capstone.Transcriber.Services;
using Capstone.Transcriber.Workers;
using Microsoft.EntityFrameworkCore;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    DotNetEnv.Env.Load(envPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AudioProcessorService>();

builder.Services.AddHostedService<AudioProcessingWorker>();

var host = builder.Build();
host.Run();
