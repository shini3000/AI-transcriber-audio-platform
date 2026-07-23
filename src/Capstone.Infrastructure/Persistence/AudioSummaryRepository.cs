using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capstone.Infrastructure.Persistence;

public class AudioSummaryRepository : IAudioSummaryRepository
{
    private readonly AppDbContext _context;

    public AudioSummaryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AudioSummary summary, CancellationToken cancellationToken = default)
    {
        await _context.AudioSummaries.AddAsync(summary, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AudioSummary?> GetByAudioFileIdAsync(Guid audioFileId, CancellationToken cancellationToken = default)
    {
        return await _context.AudioSummaries
            .FirstOrDefaultAsync(s => s.AudioFileId == audioFileId, cancellationToken);
    }
}
