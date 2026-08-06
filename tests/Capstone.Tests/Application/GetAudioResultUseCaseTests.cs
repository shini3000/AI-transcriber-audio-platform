using Capstone.Application.Interfaces;
using Capstone.Application.UseCases;
using Capstone.Domain.Entities;
using Moq;

namespace Capstone.Tests.Application;

public class GetAudioResultUseCaseTests
{
    private static readonly Guid AudioFileId = Guid.NewGuid();

    private static AudioFile CreateAudioFile() => new()
    {
        Id = AudioFileId,
        FileName = "audio.aac",
        ContentType = "audio/aac",
        Size = 1024,
        StorageUrl = "http://minio:9000/audio/audio.aac",
        UploadedAt = new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc),
        OriginalFileName = "audio.mp3",
        OriginalContentType = "audio/mpeg",
        OriginalSize = 2048
    };

    [Fact]
    public async Task ExecuteAsync_WhenAudioFileDoesNotExist_ReturnsNull()
    {
        var fileRepository = new Mock<IAudioFileRepository>();
        fileRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AudioFile?)null);
        var transcriptionRepository = new Mock<IAudioTranscriptionRepository>();

        var useCase = new GetAudioResultUseCase(fileRepository.Object, transcriptionRepository.Object);

        var result = await useCase.ExecuteAsync(AudioFileId);

        Assert.Null(result);
        transcriptionRepository.Verify(r => r.GetByAudioFileIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTranscriptionIsMissing_ReturnsPendingStatus()
    {
        var fileRepository = new Mock<IAudioFileRepository>();
        fileRepository.Setup(r => r.GetByIdAsync(AudioFileId)).ReturnsAsync(CreateAudioFile());
        var transcriptionRepository = new Mock<IAudioTranscriptionRepository>();
        transcriptionRepository.Setup(r => r.GetByAudioFileIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((AudioTranscription?)null);

        var useCase = new GetAudioResultUseCase(fileRepository.Object, transcriptionRepository.Object);

        var result = await useCase.ExecuteAsync(AudioFileId);

        Assert.NotNull(result);
        Assert.Equal("pending", result!.Status);
        Assert.Equal(AudioFileId, result.AudioFileId);
        Assert.Equal("audio.aac", result.FileName);
        Assert.Null(result.Transcription);
        Assert.Null(result.Summary);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTranscriptionExistsWithoutSummary_ReturnsPending()
    {
        var fileRepository = new Mock<IAudioFileRepository>();
        fileRepository.Setup(r => r.GetByIdAsync(AudioFileId)).ReturnsAsync(CreateAudioFile());

        var transcription = new AudioTranscription
        {
            Id = Guid.NewGuid(),
            AudioFileId = AudioFileId,
            FullText = "Hello world.",
            ModelName = "whisper-tiny",
            ProcessingTimeMs = 1234,
            CreatedAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc)
        };

        var transcriptionRepository = new Mock<IAudioTranscriptionRepository>();
        transcriptionRepository.Setup(r => r.GetByAudioFileIdAsync(AudioFileId, default)).ReturnsAsync(transcription);

        var useCase = new GetAudioResultUseCase(fileRepository.Object, transcriptionRepository.Object);

        var result = await useCase.ExecuteAsync(AudioFileId);

        Assert.NotNull(result);
        Assert.Equal("pending", result!.Status);
        Assert.Null(result.Transcription);
        Assert.Null(result.Summary);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTranscriptionAndSummaryExist_ReturnsReadyWithData()
    {
        var fileRepository = new Mock<IAudioFileRepository>();
        fileRepository.Setup(r => r.GetByIdAsync(AudioFileId)).ReturnsAsync(CreateAudioFile());

        var summary = new AudioSummary
        {
            Id = Guid.NewGuid(),
            AudioFileId = AudioFileId,
            TranscriptionId = Guid.NewGuid(),
            SummaryText = "Key meeting points",
            ModelName = "qwen2.5-0.5b",
            CreatedAt = new DateTime(2026, 7, 31, 12, 1, 0, DateTimeKind.Utc)
        };

        var transcription = new AudioTranscription
        {
            Id = Guid.NewGuid(),
            AudioFileId = AudioFileId,
            FullText = "Hello world. This is a transcription.",
            ModelName = "whisper-tiny",
            ProcessingTimeMs = 1234,
            CreatedAt = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc),
            Summary = summary
        };

        var transcriptionRepository = new Mock<IAudioTranscriptionRepository>();
        transcriptionRepository.Setup(r => r.GetByAudioFileIdAsync(AudioFileId, default)).ReturnsAsync(transcription);

        var useCase = new GetAudioResultUseCase(fileRepository.Object, transcriptionRepository.Object);

        var result = await useCase.ExecuteAsync(AudioFileId);

        Assert.NotNull(result);
        Assert.Equal("ready", result!.Status);
        Assert.Equal(AudioFileId, result.AudioFileId);
        Assert.Equal("audio.aac", result.FileName);
        Assert.NotNull(result.Transcription);
        Assert.Equal("Hello world. This is a transcription.", result.Transcription!.FullText);
        Assert.Equal("whisper-tiny", result.Transcription.ModelName);
        Assert.Equal(1234, result.Transcription.ProcessingTimeMs);
        Assert.Equal(transcription.CreatedAt, result.Transcription.CreatedAt);
        Assert.NotNull(result.Summary);
        Assert.Equal("Key meeting points", result.Summary!.SummaryText);
        Assert.Equal("qwen2.5-0.5b", result.Summary.ModelName);
        Assert.Equal(summary.CreatedAt, result.Summary.CreatedAt);
    }
}
