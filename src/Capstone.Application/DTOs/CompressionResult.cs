namespace Capstone.Application.DTOs;

public class CompressionResult
{
    public Stream CompressedStream { get; init; } = Stream.Null;
    public string CompressedContentType { get; init; } = "audio/aac";
    public long CompressedSize { get; init; }
    public long OriginalSize { get; init; }
    public TimeSpan Duration { get; init; }
    public bool WasCompressed { get; init; }
}
