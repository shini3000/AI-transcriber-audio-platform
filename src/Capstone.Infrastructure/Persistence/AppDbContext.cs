using Capstone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Capstone.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<AudioTranscription> AudioTranscriptions => Set<AudioTranscription>();
    public DbSet<AudioSummary> AudioSummaries => Set<AudioSummary>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioFile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.FileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.StorageUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Size)
                .IsRequired();

            entity.Property(e => e.UploadedAt)
                .IsRequired();

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.OriginalContentType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.OriginalSize)
                .IsRequired();

            entity.Property(e => e.CompressedAt);

            entity.Property(e => e.CompressionDurationMs);
        });

        modelBuilder.Entity<AudioTranscription>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.FullText)
                .IsRequired();

            entity.Property(e => e.ModelName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ProcessingTimeMs)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasOne(e => e.AudioFile)
                .WithMany()
                .HasForeignKey(e => e.AudioFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Summary)
                .WithOne(s => s.Transcription)
                .HasForeignKey<AudioSummary>(s => s.TranscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AudioSummary>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.SummaryText)
                .IsRequired();

            entity.Property(e => e.ModelName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasOne(e => e.AudioFile)
                .WithMany()
                .HasForeignKey(e => e.AudioFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
