using Capstone.Application.Interfaces;
using Capstone.Application.UseCases;
using Capstone.Domain.Entities;
using Moq;

namespace Capstone.Tests.Application;

public class GetAudioFilesListUseCaseTests
{
    private static readonly Guid AudioFileId1 = Guid.NewGuid();
    private static readonly Guid AudioFileId2 = Guid.NewGuid();

    [Fact]
    public async Task ExecuteAsync_ReturnsListWithCorrectStatusAndData()
    {
        var file1 = new AudioFile
        {
            Id = AudioFileId1,
            FileName = "audio1.aac",
            UploadedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var file2 = new AudioFile
        {
            Id = AudioFileId2,
            FileName = "audio2.aac",
            UploadedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var summary = new AudioSummary
        {
            Id = Guid.NewGuid(),
            AudioFileId = AudioFileId1,
            SummaryText = "Summary 1",
            ModelName = "qwen2.5",
            CreatedAt = DateTime.UtcNow
        };

        var transcription = new AudioTranscription
        {
            Id = Guid.NewGuid(),
            AudioFileId = AudioFileId1,
            FullText = "Transcription 1",
            ModelName = "whisper",
            CreatedAt = DateTime.UtcNow,
            Summary = summary
        };

        var mockData = new List<(AudioFile File, AudioTranscription? Transcription)>
        {
            (file2, null),
            (file1, transcription)
        };

        var fileRepository = new Mock<IAudioFileRepository>();
        fileRepository.Setup(r => r.GetAllWithTranscriptionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockData);

        var useCase = new GetAudioFilesListUseCase(fileRepository.Object);

        var result = await useCase.ExecuteAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var item1 = result[0];
        Assert.Equal("pending", item1.Status);
        Assert.Equal(AudioFileId2, item1.AudioFileId);
        Assert.Null(item1.Transcription);
        Assert.Null(item1.Summary);

        var item2 = result[1];
        Assert.Equal("ready", item2.Status);
        Assert.Equal(AudioFileId1, item2.AudioFileId);
        Assert.NotNull(item2.Transcription);
        Assert.Equal("Transcription 1", item2.Transcription!.FullText);
        Assert.NotNull(item2.Summary);
        Assert.Equal("Summary 1", item2.Summary!.SummaryText);
    }
}
