using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;

namespace Capstone.Infrastructure.Persistence;

public class AudioFileRepository : IAudioFileRepository
{
    private readonly AppDbContext _context;

    public AudioFileRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AudioFile audioFile, CancellationToken cancellationToken = default)
    {
        await _context.AudioFiles.AddAsync(audioFile, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
