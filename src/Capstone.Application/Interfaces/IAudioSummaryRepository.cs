using Capstone.Domain.Entities;

namespace Capstone.Application.Interfaces;

public interface IAudioSummaryRepository
{
    Task AddAsync(AudioSummary summary, CancellationToken cancellationToken = default);
    Task<AudioSummary?> GetByAudioFileIdAsync(Guid audioFileId, CancellationToken cancellationToken = default);
}
