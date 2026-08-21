using Capstone.Application.Interfaces;
using Capstone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task AddRangeAsync(IEnumerable<AudioFile> audioFiles, CancellationToken cancellationToken = default)
    {
        await _context.AudioFiles.AddRangeAsync(audioFiles, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AudioFile?> GetByIdAsync(Guid id)
    {
        return await _context.AudioFiles
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<bool> MarkProcessedAsync(Guid id, DateTime processedAt)
    {
        var audioFile = await _context.AudioFiles
            .FirstOrDefaultAsync(f => f.Id == id);

        if (audioFile is null)
            return false;

        audioFile.ProcessedAt = processedAt;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task UpdateAsync(AudioFile audioFile, CancellationToken cancellationToken = default)
    {
        _context.AudioFiles.Update(audioFile);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<(AudioFile File, AudioTranscription? Transcription)>> GetAllWithTranscriptionAsync(CancellationToken cancellationToken = default)
    {
        var query = from file in _context.AudioFiles
                    where file.Status != "rejected"
                    join tx in _context.AudioTranscriptions.Include(t => t.Summary)
                        on file.Id equals tx.AudioFileId into txGroup
                    from subTx in txGroup.DefaultIfEmpty()
                    orderby file.UploadedAt descending
                    select new { File = file, Transcription = subTx };

        var results = await query.ToListAsync(cancellationToken);

        return results.Select(r => (r.File, (AudioTranscription?)r.Transcription)).ToList();
    }
}
