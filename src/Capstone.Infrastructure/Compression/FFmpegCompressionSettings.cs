namespace Capstone.Infrastructure.Compression;

public class FFmpegCompressionSettings
{
    public const string SectionName = "FFmpeg";

    public string FfmpegPath { get; set; } = "ffmpeg";
    public int BitRate { get; set; } = 128000;
    public int CompressionTimeoutMs { get; set; } = 120_000;
}
