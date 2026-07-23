using System.Diagnostics;
using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capstone.Infrastructure.Compression;

public class FFmpegCompressionService : IAudioCompressionService
{
    private readonly FFmpegCompressionSettings _settings;
    private readonly ILogger<FFmpegCompressionService> _logger;

    public FFmpegCompressionService(
        IOptions<FFmpegCompressionSettings> settings,
        ILogger<FFmpegCompressionService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CompressionResult> CompressAsync(
        Stream inputStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var originalSize = inputStream.Length;

        if (contentType.Equals("audio/aac", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("audio/mp4", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Input is already AAC — skipping compression");
            sw.Stop();
            return new CompressionResult
            {
                CompressedStream = inputStream,
                CompressedContentType = contentType,
                CompressedSize = originalSize,
                OriginalSize = originalSize,
                Duration = sw.Elapsed,
                WasCompressed = false
            };
        }

        var outputStream = new MemoryStream();
        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.FfmpegPath,
            Arguments = $"-i pipe:0 -c:a aac -b:a {_settings.BitRate} -f adts pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdinTask = inputStream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync();

        await stdinTask;
        process.StandardInput.Close();

        var exitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(_settings.CompressionTimeoutMs, cancellationToken);
        var completed = await Task.WhenAny(exitTask, timeoutTask);

        if (completed == timeoutTask)
        {
            process.Kill(entireProcessTree: true);
            sw.Stop();
            _logger.LogError("FFmpeg compression timed out after {Duration}ms", sw.ElapsedMilliseconds);
            throw new TimeoutException($"FFmpeg compression timed out after {_settings.CompressionTimeoutMs}ms");
        }

        await outputTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            sw.Stop();
            _logger.LogError("FFmpeg exited with code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException($"FFmpeg compression failed (exit code {process.ExitCode}): {stderr}");
        }

        sw.Stop();
        outputStream.Position = 0;

        _logger.LogInformation(
            "Audio compressed in {Duration}ms: {OriginalSize} → {CompressedSize} bytes ({Ratio:P0} reduction)",
            sw.ElapsedMilliseconds,
            originalSize,
            outputStream.Length,
            originalSize > 0 ? (double)(originalSize - outputStream.Length) / originalSize : 0);

        return new CompressionResult
        {
            CompressedStream = outputStream,
            CompressedContentType = "audio/aac",
            CompressedSize = outputStream.Length,
            OriginalSize = originalSize,
            Duration = sw.Elapsed,
            WasCompressed = true
        };
    }
}
