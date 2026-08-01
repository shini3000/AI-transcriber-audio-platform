using System.Diagnostics;
using Whisper.net;

namespace Capstone.Transcriber.Services;

public class AudioTranscriptionService
{
    private readonly ILogger<AudioTranscriptionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TranscriptionModels _models;

    public AudioTranscriptionService(
        ILogger<AudioTranscriptionService> logger,
        IConfiguration configuration,
        TranscriptionModels models)
    {
        _logger = logger;
        _configuration = configuration;
        _models = models;
    }

    public async Task<string> TranscribeAsync(string audioPath)
    {
        var ffmpegPath = _configuration["FFmpeg:FfmpegPath"] ?? "ffmpeg";

        var wavPath = Path.ChangeExtension(audioPath, ".wav");
        _logger.LogDebug("Converting AAC => WAV: ffmpeg={FfmpegPath}, input={AudioPath}, output={WavPath}",
            ffmpegPath, audioPath, wavPath);

        var convertSw = Stopwatch.StartNew();
        var convertStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{audioPath}\" -ar 16000 -ac 1 -c:a pcm_s16le -y \"{wavPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var convertProcess = new Process { StartInfo = convertStartInfo })
        {
            convertProcess.Start();
            await convertProcess.WaitForExitAsync();
            var stderr = await convertProcess.StandardError.ReadToEndAsync();

            convertSw.Stop();

            if (convertProcess.ExitCode != 0)
            {
                _logger.LogError("FFmpeg AAC => WAV conversion failed (exit {ExitCode}): {Stderr}",
                    convertProcess.ExitCode, stderr);
                throw new InvalidOperationException(
                    $"FFmpeg AAC => WAV conversion failed: {stderr}");
            }

            _logger.LogInformation(
                "AAC => WAV conversion done: {Input} => {Output} (took {Ms}ms)",
                Path.GetFileName(audioPath), Path.GetFileName(wavPath), convertSw.ElapsedMilliseconds);
        }

        try
        {
            using var whisperGate = await _models.AcquireWhisperAsync();
            using var whisperProcessor = _models.GetWhisperFactory().CreateBuilder()
                .WithLanguage("en")
                .Build();

            _logger.LogDebug("Starting Whisper transcription from WAV: {WavPath}", wavPath);

            var txSw = Stopwatch.StartNew();
            var segments = new List<string>();

            await using var wavStream = File.OpenRead(wavPath);
            await foreach (var segment in whisperProcessor.ProcessAsync(wavStream))
            {
                segments.Add(segment.Text);
                _logger.LogTrace("Whisper segment: {Segment}", segment.Text);
            }

            txSw.Stop();
            _logger.LogInformation(
                "Whisper transcription finished: {SegmentCount} segments, took {Ms}ms",
                segments.Count, txSw.ElapsedMilliseconds);

            return string.Join(" ", segments);
        }
        finally
        {
            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
                _logger.LogDebug("Temporary WAV deleted: {WavPath}", wavPath);
            }
        }
    }
}
