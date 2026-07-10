namespace Capstone.Application.DTOs;

public class UploadAudioRequest
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
    public Stream FileStream { get; init; } = Stream.Null;
}
