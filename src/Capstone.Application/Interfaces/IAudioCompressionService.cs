using Capstone.Application.DTOs;

namespace Capstone.Application.Interfaces;

public interface IAudioCompressionService
{
    Task<CompressionResult> CompressAsync(
        Stream inputStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
