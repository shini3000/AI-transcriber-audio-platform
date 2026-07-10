using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;

namespace Capstone.Application.UseCases;

public class UploadAudioUseCase
{
    private readonly IFileStorageService _storageService;
    private readonly IAudioFileRepository _repository;

    public UploadAudioUseCase(IFileStorageService storageService, IAudioFileRepository repository)
    {
        _storageService = storageService;
        _repository = repository;
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
            url
        );

        await _repository.AddAsync(audioFile, cancellationToken);

        return new UploadAudioResponse(
            audioFile.Id,
            audioFile.FileName,
            audioFile.StorageUrl,
            audioFile.UploadedAt
        );
    }
}
