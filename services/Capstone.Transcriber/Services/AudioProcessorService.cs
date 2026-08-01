using System.Diagnostics;

namespace Capstone.Transcriber.Services;

public class AudioProcessorService
{
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly AudioFileDownloader _downloader;
    private readonly AudioTranscriptionService _transcriptionService;
    private readonly AudioSummarizationService _summarizationService;

    private readonly SemaphoreSlim _dbGate = new(1, 1);

    public AudioProcessorService(
        ILogger<AudioProcessorService> logger,
        AppDbContext dbContext,
        AudioFileDownloader downloader,
        AudioTranscriptionService transcriptionService,
        AudioSummarizationService summarizationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _downloader = downloader;
        _transcriptionService = transcriptionService;
        _summarizationService = summarizationService;
    }

    public async Task ProcessAsync(AudioUploadedMessage message)
    {
        var totalSw = Stopwatch.StartNew();
        _logger.LogInformation(
            "===== PROCESSING START: AudioFileId={AudioFileId}, FileName={FileName}, StorageUrl={StorageUrl} =====",
            message.AudioFileId, message.FileName, message.StorageUrl);

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{message.FileName}");
        try
        {
            var dlSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 1/5] Downloading audio from MinIO: {FileName}", message.FileName);
            await _downloader.DownloadFromMinioAsync(message.StorageUrl, tempFile);
            dlSw.Stop();
            _logger.LogInformation("[STEP 1/5] Download complete: {FileName} ({Size} bytes, took {Ms}ms)",
                message.FileName, new FileInfo(tempFile).Length, dlSw.ElapsedMilliseconds);

            var txSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 2/5] Transcribing audio with Whisper: {FileName}", message.FileName);
            var fullText = await _transcriptionService.TranscribeAsync(tempFile);
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

            if (!await _dbGate.WaitAsync(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Timed out waiting for the shared DbContext gate (transcription save)");
            try
            {
                _dbContext.AudioTranscriptions.Add(transcription);
                await _dbContext.SaveChangesAsync();
            }
            finally
            {
                _dbGate.Release();
            }
            dbSw.Stop();
            _logger.LogInformation("[STEP 3/5] Transcription saved (Id={Id}, took {Ms}ms)",
                transcription.Id, dbSw.ElapsedMilliseconds);

            var smSw = Stopwatch.StartNew();
            _logger.LogInformation("[STEP 4/5] Generating summary with LLamaSharp (Qwen)...");
            var summaryText = await _summarizationService.GenerateSummaryAsync(fullText);
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

            if (!await _dbGate.WaitAsync(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Timed out waiting for the shared DbContext gate (summary save)");
            try
            {
                _dbContext.AudioSummaries.Add(summary);
                await _dbContext.SaveChangesAsync();
            }
            finally
            {
                _dbGate.Release();
            }

            if (!await _dbGate.WaitAsync(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Timed out waiting for the shared DbContext gate (change tracker clear)");
            try
            {
                _dbContext.ChangeTracker.Clear();
            }
            finally
            {
                _dbGate.Release();
            }

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
}
