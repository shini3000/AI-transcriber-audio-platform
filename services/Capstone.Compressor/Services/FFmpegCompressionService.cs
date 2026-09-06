using System.Diagnostics;

namespace Capstone.Compressor.Services;

public class FFmpegCompressionService
{
    private readonly ILogger<FFmpegCompressionService> _logger;
    private readonly IConfiguration _configuration;

    public FFmpegCompressionService(
        ILogger<FFmpegCompressionService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<(string OutputPath, TimeSpan Duration, long CompressedSize)> CompressToAacAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        var ffmpegPath = _configuration["FFmpeg:FfmpegPath"] ?? "ffmpeg";
        var bitrate = _configuration["FFmpeg:BitRate"] ?? "128k";
        var audioFilter = _configuration["FFmpeg:AudioFilter"] ?? "highpass=f=400,lowpass=f=3000,equalizer=f=1200:t=q:w=1.2:g=8,volume=1.8";
        var outputPath = Path.ChangeExtension(inputPath, ".aac");
        if (outputPath == inputPath)
        {
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, $"compressed_{Path.GetFileName(inputPath)}");
        }

        var sw = Stopwatch.StartNew();
        var filterArg = string.IsNullOrWhiteSpace(audioFilter) ? string.Empty : $"-af \"{audioFilter}\" ";
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-y -threads 1 -vn -nostats -loglevel error -i \"{inputPath}\" {filterArg}-c:a aac -b:a {bitrate} \"{outputPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;

        sw.Stop();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg compression failed (exit {ExitCode}): {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"FFmpeg compression failed: {stderr}");
        }

        var compressedSize = new FileInfo(outputPath).Length;
        _logger.LogInformation("Audio compressed to AAC: {Output} ({Size} bytes, took {Ms}ms)",
            Path.GetFileName(outputPath), compressedSize, sw.ElapsedMilliseconds);

        return (outputPath, sw.Elapsed, compressedSize);
    }
}
