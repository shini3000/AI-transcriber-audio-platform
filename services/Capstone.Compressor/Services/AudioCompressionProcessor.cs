using System.Diagnostics;
using Capstone.Compressor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capstone.Compressor.Services;

public class AudioCompressionProcessor
{
    private readonly ILogger<AudioCompressionProcessor> _logger;
    private readonly AppDbContext _dbContext;
    private readonly AudioFileDownloader _downloader;
    private readonly FFmpegCompressionService _compressionService;
    private readonly MinIOStorageService _storageService;

    public AudioCompressionProcessor(
        ILogger<AudioCompressionProcessor> logger,
        AppDbContext dbContext,
        AudioFileDownloader downloader,
        FFmpegCompressionService compressionService,
        MinIOStorageService storageService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _downloader = downloader;
        _compressionService = compressionService;
        _storageService = storageService;
    }

    public async Task<string> ProcessAsync(AudioUploadedMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("===== COMPRESSION START: AudioFileId={AudioFileId}, FileName={FileName}, StorageUrl={StorageUrl} =====",
            message.AudioFileId, message.FileName, message.StorageUrl);

        var tempRawFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{message.FileName}");
        string? tempCompressedFile = null;

        try
        {
            _logger.LogInformation("[STEP 1/3] Downloading raw audio from MinIO: {FileName}", message.FileName);
            await _downloader.DownloadFromMinioAsync(message.StorageUrl, tempRawFile);

            _logger.LogInformation("[STEP 2/3] Compressing audio with FFmpeg to .aac: {FileName}", message.FileName);
            var (outputPath, duration, compressedSize) = await _compressionService.CompressToAacAsync(tempRawFile, cancellationToken);
            tempCompressedFile = outputPath;

            var compressedFileName = Path.ChangeExtension(message.FileName, ".aac");

            _logger.LogInformation("[STEP 3/3] Uploading compressed .aac to MinIO: {FileName}", compressedFileName);
            var compressedStorageUrl = await _storageService.UploadFileAsync(
                tempCompressedFile,
                compressedFileName,
                "audio/aac",
                cancellationToken);

            var audioFile = await _dbContext.AudioFiles.FirstOrDefaultAsync(f => f.Id == message.AudioFileId, cancellationToken);
            if (audioFile != null)
            {
                audioFile.FileName = compressedFileName;
                audioFile.ContentType = "audio/aac";
                audioFile.Size = compressedSize;
                audioFile.StorageUrl = compressedStorageUrl;
                audioFile.CompressedAt = DateTime.UtcNow;
                audioFile.CompressionDurationMs = (long)duration.TotalMilliseconds;

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated database for AudioFileId={AudioFileId}", message.AudioFileId);
            }

            _logger.LogInformation("===== COMPRESSION COMPLETE: AudioFileId={AudioFileId}, CompressedUrl={CompressedStorageUrl} =====",
                message.AudioFileId, compressedStorageUrl);

            return compressedStorageUrl;
        }
        finally
        {
            if (File.Exists(tempRawFile))
            {
                try { File.Delete(tempRawFile); } catch { }
            }
            if (tempCompressedFile != null && File.Exists(tempCompressedFile))
            {
                try { File.Delete(tempCompressedFile); } catch { }
            }
        }
    }
}
