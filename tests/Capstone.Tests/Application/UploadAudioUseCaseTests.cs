using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Capstone.Application.UseCases;
using Capstone.Domain.Entities;
using Moq;

namespace Capstone.Tests.Application;

public class UploadAudioUseCaseTests
{
    private readonly Mock<IFileStorageService> _storageServiceMock = new();
    private readonly Mock<IAudioFileRepository> _repositoryMock = new();
    private readonly Mock<IAudioEventPublisher> _eventPublisherMock = new();

    private UploadAudioUseCase CreateUseCase() =>
        new(
            _storageServiceMock.Object,
            _repositoryMock.Object,
            _eventPublisherMock.Object);

    [Fact]
    public async Task ExecuteAsync_UploadsToStoragePersistsRecordAndPublishesEvent()
    {
        var useCase = CreateUseCase();
        _storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://minio:9000/audio/sample.mp3");

        var request = new UploadAudioRequest
        {
            FileName = "sample.mp3",
            ContentType = "audio/mpeg",
            Size = 2048,
            FileStream = new MemoryStream(new byte[100])
        };

        var response = await useCase.ExecuteAsync(request);

        Assert.NotNull(response);
        Assert.Equal("sample.mp3", response.FileName);
        Assert.Equal("http://minio:9000/audio/sample.mp3", response.StorageUrl);
        Assert.NotEqual(Guid.Empty, response.Id);

        _storageServiceMock.Verify(s => s.UploadAsync(It.IsAny<Stream>(), "sample.mp3", "audio/mpeg", It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.AddAsync(It.Is<AudioFile>(f => f.FileName == "sample.mp3"), It.IsAny<CancellationToken>()), Times.Once);
        _eventPublisherMock.Verify(e => e.PublishAudioUploadedAsync(It.IsAny<Guid>(), "http://minio:9000/audio/sample.mp3", "sample.mp3", It.IsAny<CancellationToken>()), Times.Once);
    }
}
