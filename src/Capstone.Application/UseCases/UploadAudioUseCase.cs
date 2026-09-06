using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;

namespace Capstone.Application.UseCases;

public class UploadAudioUseCase
{
    private readonly IFileStorageService _storageService;
    private readonly IAudioFileRepository _repository;
    private readonly IAudioEventPublisher _eventPublisher;

    public UploadAudioUseCase(
        IFileStorageService storageService,
        IAudioFileRepository repository,
        IAudioEventPublisher eventPublisher)
    {
        _storageService = storageService;
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<UploadAudioResponse> ExecuteAsync(UploadAudioRequest request, CancellationToken cancellationToken = default)
    {
        var url = await _storageService.UploadAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            cancellationToken
        );

        var audioFile = AudioFile.Create(
            request.FileName,
            request.ContentType,
            request.Size,
            url,
            originalFileName: request.FileName,
            originalContentType: request.ContentType,
            originalSize: request.Size,
            originalStorageUrl: url
        );

        await _repository.AddAsync(audioFile, cancellationToken);

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
            CompressedSize = request.Size,
            WasCompressed = false
        };
    }
}
