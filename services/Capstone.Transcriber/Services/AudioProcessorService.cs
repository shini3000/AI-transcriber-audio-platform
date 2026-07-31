using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Abstractions;
using LLama.Sampling;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Whisper.net;
using Capstone.Transcriber;

namespace Capstone.Transcriber.Services;

public class AudioProcessorService
{
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly WhisperFactory _whisperFactory;

    public AudioProcessorService(
        ILogger<AudioProcessorService> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        AppDbContext dbContext)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _dbContext = dbContext;

        _minioClient = new MinioClient()
            .WithEndpoint(configuration["MinIO:Endpoint"] ?? "localhost:9000")
            .WithCredentials(
                configuration["MinIO:AccessKey"] ?? "minioadmin",
                configuration["MinIO:SecretKey"] ?? "minioadmin")
            .WithSSL(bool.Parse(configuration["MinIO:UseSsl"] ?? "false"))
            .Build();

        var whisperModelPath = configuration["Model:WhisperModelPath"] ?? "tiny";
        if (whisperModelPath != "tiny" && !File.Exists(whisperModelPath))
        {
            var absolutePath = Path.GetFullPath(whisperModelPath);
            throw new FileNotFoundException($"Whisper model file not found. Checked path: {absolutePath}");
        }
        _whisperFactory = WhisperFactory.FromPath(whisperModelPath);
    }

    public async Task ProcessAsync(AudioUploadedMessage message, CancellationToken cancellationToken)
    {
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation(
            "===== PROCESSING START: AudioFileId={AudioFileId}, FileName={FileName}, StorageUrl={StorageUrl} =====",
            message.AudioFileId, message.FileName, message.StorageUrl);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{message.FileName}");
        try
        {
            var dlSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 1/5] Downloading audio from MinIO: {FileName}", message.FileName);
            await DownloadFromMinioAsync(message.StorageUrl, tempFile, cancellationToken);
            dlSw.Stop();
            _logger.LogInformation("[STEP 1/5] Download complete: {FileName} ({Size} bytes, took {Ms}ms)",
                message.FileName, new FileInfo(tempFile).Length, dlSw.ElapsedMilliseconds);

            var txSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 2/5] Transcribing audio with Whisper: {FileName}", message.FileName);
            var fullText = await TranscribeAsync(tempFile, cancellationToken);
            txSw.Stop();
            _logger.LogInformation("[STEP 2/5] Transcription complete: {Length} chars, took {Ms}ms",
                fullText.Length, txSw.ElapsedMilliseconds);

            var dbSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 3/5] Saving transcription to database...");
            var dbTimestamp = DateTime.UtcNow;
            var transcription = new Domain.Entities.AudioTranscription
            {
                Id = Guid.NewGuid(),
                AudioFileId = message.AudioFileId,
                FullText = fullText,
                ModelName = "whisper-tiny",
                ProcessingTimeMs = txSw.ElapsedMilliseconds,
                CreatedAt = dbTimestamp
            };

            _dbContext.AudioTranscriptions.Add(transcription);
            await _dbContext.SaveChangesAsync(cancellationToken);
            dbSw.Stop();
            _logger.LogInformation("[STEP 3/5] Transcription saved (Id={Id}, took {Ms}ms)",
                transcription.Id, dbSw.ElapsedMilliseconds);

            var smSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 4/5] Generating summary with LLamaSharp (Qwen)...");
            var summaryText = await GenerateSummaryAsync(fullText, cancellationToken);
            smSw.Stop();
            _logger.LogInformation("[STEP 4/5] Summary generated: '{Summary}' (took {Ms}ms)",
                summaryText, smSw.ElapsedMilliseconds);

            _logger.LogInformation("[STEP 5/5] Saving summary to database...");
            var summary = new Domain.Entities.AudioSummary
            {
                Id = Guid.NewGuid(),
                AudioFileId = message.AudioFileId,
                TranscriptionId = transcription.Id,
                SummaryText = summaryText,
                ModelName = "qwen2.5-0.5b-q8_0",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AudioSummaries.Add(summary);
            await _dbContext.SaveChangesAsync(cancellationToken);

            totalSw.Stop();
            _logger.LogInformation(
                "===== PROCESSING COMPLETE: AudioFileId={AudioFileId}, FileName={FileName}, Total={TotalMs}ms " +
                "(Download={DlMs}ms, Transcribe={TxMs}ms, Summary={SmMs}ms) =====",
                message.AudioFileId, message.FileName,
                totalSw.ElapsedMilliseconds, dlSw.ElapsedMilliseconds,
                txSw.ElapsedMilliseconds, smSw.ElapsedMilliseconds);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
                _logger.LogDebug("Temporary file deleted: {TempFile}", tempFile);
            }
        }
    }

    private async Task DownloadFromMinioAsync(string storageUrl, string tempPath, CancellationToken cancellationToken)
    {
        var uri = new Uri(storageUrl);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/');
        var bucketName = segments[0];
        var objectName = string.Join("/", segments[1..]);

        _logger.LogDebug("Downloading from MinIO: bucket={Bucket}, object={Object} → {TempPath}",
            bucketName, objectName, tempPath);

        await using var fileStream = File.Create(tempPath);
        await _minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(fileStream)),
            cancellationToken);
    }

    private async Task<string> TranscribeAsync(string audioPath, CancellationToken cancellationToken)
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
            await convertProcess.WaitForExitAsync(cancellationToken);
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
            using var whisperProcessor = _whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            _logger.LogDebug("Starting Whisper transcription from WAV: {WavPath}", wavPath);

            var txSw = Stopwatch.StartNew();
            var segments = new List<string>();

            await using var wavStream = File.OpenRead(wavPath);
            await foreach (var segment in whisperProcessor.ProcessAsync(wavStream, cancellationToken))
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

    private async Task<string> GenerateSummaryAsync(string text, CancellationToken cancellationToken)
    {
        var modelPath = _configuration["Model:QwenPath"]
            ?? throw new InvalidOperationException("Model:QwenPath not configured");

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Qwen model not found at {Path}. Using first 50 chars as fallback.", modelPath);
            return TruncateToFirstSentence(text, 50);
        }

        try
        {
            _logger.LogDebug("Loading Qwen model from {Path}...", modelPath);

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 1024,
                GpuLayerCount = 0
            };

            using var model = LLamaWeights.LoadFromFile(parameters);
            _logger.LogDebug("Qwen model loaded ({SizeInBytes} bytes)", model.SizeInBytes);

            using var context = model.CreateContext(parameters, _loggerFactory.CreateLogger<LLamaContext>());
            var executor = new InteractiveExecutor(context, _loggerFactory.CreateLogger<InteractiveExecutor>());

            _logger.LogDebug("Running inference for summary generation...");

            var prompt = $@"<|system|>
You are a title generator. Given a conversation transcript, generate a short title (max 50 characters) that captures the main topic, like ChatGPT conversation titles.

<|user|>
{text}

<|assistant|>";

            var result = string.Empty;
            var inferenceParams = new InferenceParams
            {
                MaxTokens = 64,
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.3f }
            };

            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                result += token;
                if (result.Contains('\n') || result.Length > 100)
                    break;
            }

            result = result.Trim().TrimEnd('.', '!', '?');
            return TruncateToFirstSentence(result, 50);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLamaSharp failed, using fallback truncation");
            return TruncateToFirstSentence(text, 50);
        }
    }

    private static string TruncateToFirstSentence(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Untitled";

        var cleaned = text.Trim();
        if (cleaned.Length <= maxLength)
            return cleaned;

        var endIdx = cleaned.IndexOfAny(['.', '!', '?', '\n']);
        if (endIdx > 0 && endIdx <= maxLength)
            return cleaned[..endIdx].Trim();

        return cleaned[..maxLength].Trim();
    }
}
