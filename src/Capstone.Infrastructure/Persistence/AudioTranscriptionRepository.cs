using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capstone.Infrastructure.Persistence;

public class AudioTranscriptionRepository : IAudioTranscriptionRepository
{
    private readonly AppDbContext _context;

    public AudioTranscriptionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AudioTranscription transcription, CancellationToken cancellationToken = default)
    {
        await _context.AudioTranscriptions.AddAsync(transcription, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AudioTranscription?> GetByAudioFileIdAsync(Guid audioFileId, CancellationToken cancellationToken = default)
    {
        return await _context.AudioTranscriptions
            .Include(t => t.Summary)
            .FirstOrDefaultAsync(t => t.AudioFileId == audioFileId, cancellationToken);
    }
}
