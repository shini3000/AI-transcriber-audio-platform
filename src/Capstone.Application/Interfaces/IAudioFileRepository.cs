using Capstone.Domain.Entities;

namespace Capstone.Application.Interfaces;

/// <summary>
/// Puerto para persistencia de metadatos de archivos de audio.
/// </summary>
public interface IAudioFileRepository
{
    Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken = default);
    Task<AudioFile?> GetByIdAsync(Guid id);
    Task<bool> MarkProcessedAsync(Guid id, DateTime processedAt);
}
