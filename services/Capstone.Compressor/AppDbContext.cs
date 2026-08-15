using Microsoft.EntityFrameworkCore;
using Capstone.Compressor.Domain.Entities;

namespace Capstone.Compressor;

public class AppDbContext : DbContext
{
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioFile>(entity =>
        {
            entity.ToTable("AudioFiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Size).IsRequired();
            entity.Property(e => e.StorageUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.UploadedAt).IsRequired();
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OriginalSize).IsRequired();
        });
    }
}
