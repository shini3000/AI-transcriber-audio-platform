using Capstone.Domain.Entities;

namespace Capstone.Application.Interfaces;

public interface IAudioFileRepository
{
    Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken = default);
    Task<AudioFile?> GetByIdAsync(Guid id);
    Task<bool> MarkProcessedAsync(Guid id, DateTime processedAt);
    Task<List<(AudioFile File, AudioTranscription? Transcription)>> GetAllWithTranscriptionAsync(CancellationToken cancellationToken = default);
}
