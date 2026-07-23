using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;

namespace Capstone.Application.UseCases;

public class UploadAudioUseCase
{
    private readonly IFileStorageService _storageService;
    private readonly IAudioFileRepository _repository;
    private readonly IAudioCompressionService _compressionService;
    private readonly IAudioEventPublisher _eventPublisher;

    public UploadAudioUseCase(
        IFileStorageService storageService,
        IAudioFileRepository repository,
        IAudioCompressionService compressionService,
        IAudioEventPublisher eventPublisher)
    {
        _storageService = storageService;
        _repository = repository;
        _compressionService = compressionService;
        _eventPublisher = eventPublisher;
    }

    public async Task<UploadAudioResponse> ExecuteAsync(UploadAudioRequest request, CancellationToken cancellationToken = default)
    {
        var compressionResult = await _compressionService.CompressAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            cancellationToken);

        await using var compressedStream = compressionResult.CompressedStream;

        var compressedFileName = compressionResult.WasCompressed
            ? Path.ChangeExtension(request.FileName, ".aac")
            : request.FileName;

        var compressedContentType = compressionResult.WasCompressed
            ? "audio/aac"
            : request.ContentType;

        var url = await _storageService.UploadAsync(
            compressedStream,
            compressedFileName,
            compressedContentType,
            cancellationToken
        );

        var audioFile = AudioFile.Create(
            compressedFileName,
            compressedContentType,
            compressionResult.CompressedSize,
            url,
            originalFileName: request.FileName,
            originalContentType: request.ContentType,
            originalSize: compressionResult.OriginalSize
        );

        if (compressionResult.WasCompressed)
        {
            audioFile.CompressionDurationMs = (long)compressionResult.Duration.TotalMilliseconds;
        }

        await _repository.AddAsync(audioFile, cancellationToken);

        // Publicar evento para que el worker procese transcripción + resumen
        await _eventPublisher.PublishAudioUploadedAsync(
            audioFile.Id,
            audioFile.StorageUrl,
            audioFile.FileName,
            cancellationToken);

        return new UploadAudioResponse(
            audioFile.Id,
            audioFile.FileName,
            audioFile.StorageUrl,
            audioFile.UploadedAt
        )
        {
            CompressedSize = compressionResult.CompressedSize,
            WasCompressed = compressionResult.WasCompressed
        };
    }
}
