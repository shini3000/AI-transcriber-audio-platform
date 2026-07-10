using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Capstone.Infrastructure.Persistence;


public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connectionString = ResolveConnectionString(args);

        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString(string[] args)
    {
        if (args is { Length: > 0 } && !string.IsNullOrWhiteSpace(args[0]))
            return args[0];

        var envFile = FindEnvFile();
        if (envFile is not null)
        {
            LoadEnvFile(envFile);
        }

        var envVar = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(envVar))
            return ResolveEnvValue(envVar);

        Console.Error.WriteLine("No connection string found. Using localhost fallback. " +
                                "Set ConnectionStrings__DefaultConnection in .env or as env var.");
        return "Host=localhost;Database=capstone_audio;Username=postgres;Password=postgres";
    }

    private static void LoadEnvFile(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim('"', '\'');
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, val);
                }
            }
        }
    }

    private static string ResolveEnvValue(string raw)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"\$\{([^}]+)\}",
            match =>
            {
                var varName = match.Groups[1].Value;
                return Environment.GetEnvironmentVariable(varName) ?? match.Value;
            });
    }

    private static string? FindEnvFile()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var current = dir; current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
