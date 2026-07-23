namespace Capstone.Application.Interfaces;

public interface IAudioEventPublisher
{
    Task PublishAudioUploadedAsync(Guid audioFileId, string storageUrl, string fileName, CancellationToken cancellationToken = default);
}
