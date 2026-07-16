using Capstone.Application.DTOs;
using Capstone.Application.Interfaces;
using Capstone.Infrastructure.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.ComponentModel;
using System.Text;

namespace Capstone.Tests.Compression;

public class FFmpegCompressionServiceTests
{
    private readonly FFmpegCompressionSettings _settings;
    private readonly Mock<IOptions<FFmpegCompressionSettings>> _optionsMock;
    private readonly Mock<ILogger<FFmpegCompressionService>> _loggerMock;

    public FFmpegCompressionServiceTests()
    {
        _settings = new FFmpegCompressionSettings
        {
            FfmpegPath = "ffmpeg",
            BitRate = 128000,
            CompressionTimeoutMs = 5000
        };
        _optionsMock = new Mock<IOptions<FFmpegCompressionSettings>>();
        _optionsMock.Setup(o => o.Value).Returns(_settings);
        _loggerMock = new Mock<ILogger<FFmpegCompressionService>>();
    }

    [Fact]
    public async Task CompressAsync_WhenInputIsAAC_ReturnsPassthrough()
    {
        var service = new FFmpegCompressionService(_optionsMock.Object, _loggerMock.Object);
        var inputStream = new MemoryStream(Encoding.UTF8.GetBytes("fake aac data"));

        var result = await service.CompressAsync(inputStream, "test.aac", "audio/aac");

        Assert.False(result.WasCompressed);
        Assert.Equal(inputStream.Length, result.CompressedSize);
        Assert.Equal("audio/aac", result.CompressedContentType);
        Assert.Same(inputStream, result.CompressedStream);
    }

    [Fact]
    public async Task CompressAsync_WhenInputIsMP4_ReturnsPassthrough()
    {
        var service = new FFmpegCompressionService(_optionsMock.Object, _loggerMock.Object);
        var inputStream = new MemoryStream(Encoding.UTF8.GetBytes("fake mp4 data"));

        var result = await service.CompressAsync(inputStream, "test.mp4", "audio/mp4");
        Assert.False(result.WasCompressed);
        Assert.Equal(inputStream.Length, result.CompressedSize);
    }

    [Fact]
    public void CompressionResult_DefaultContentType_IsAudioAac()
    {
        var result = new CompressionResult();
        Assert.Equal("audio/aac", result.CompressedContentType);
    }

    [Fact]
    public void CompressionResult_DefaultStream_IsNull()
    {
        var result = new CompressionResult();
        Assert.Equal(Stream.Null, result.CompressedStream);
    }

    [Fact]
    public async Task CompressAsync_WhenFfmpegNotInstalled_ThrowsInvalidOperationException()
    {
        var badSettings = new FFmpegCompressionSettings
        {
            FfmpegPath = "ffmpeg-nonexistent",
            BitRate = 128000,
            CompressionTimeoutMs = 1000
        };
        var badOptionsMock = new Mock<IOptions<FFmpegCompressionSettings>>();
        badOptionsMock.Setup(o => o.Value).Returns(badSettings);
        var service = new FFmpegCompressionService(badOptionsMock.Object, _loggerMock.Object);
        var inputStream = new MemoryStream(Encoding.UTF8.GetBytes("test audio data"));

        await Assert.ThrowsAsync<Win32Exception>(() =>
            service.CompressAsync(inputStream, "test.wav", "audio/wav"));
    }
}
