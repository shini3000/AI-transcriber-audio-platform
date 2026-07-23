using Capstone.Domain.Entities;

namespace Capstone.Application.Interfaces;

public interface IAudioTranscriptionRepository
{
    Task AddAsync(AudioTranscription transcription, CancellationToken cancellationToken = default);
    Task<AudioTranscription?> GetByAudioFileIdAsync(Guid audioFileId, CancellationToken cancellationToken = default);
}
