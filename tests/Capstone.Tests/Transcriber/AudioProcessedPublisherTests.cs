using System.Text;
using System.Text.Json;
using Capstone.Transcriber;
using Capstone.Transcriber.Messaging;
using Moq;
using RabbitMQ.Client;

namespace Capstone.Tests.Transcriber;

public class AudioProcessedPublisherTests
{
    [Fact]
    public async Task PublishAsync_PublishesPascalCaseProcessedEventWithExpectedExchangeAndRoutingKey()
    {
        var audioFileId = Guid.NewGuid();
        var channel = new Mock<IChannel>();
        var publisher = new AudioProcessedPublisher();

        await publisher.PublishAsync(channel.Object, audioFileId, "audio.processed", "capstone.audio");

        var invocation = Assert.Single(channel.Invocations);
        Assert.Equal("capstone.audio", invocation.Arguments[0]);
        Assert.Equal("audio.processed", invocation.Arguments[1]);
        Assert.False((bool)invocation.Arguments[2]);
        var body = (ReadOnlyMemory<byte>)invocation.Arguments[4];

        var json = Encoding.UTF8.GetString(body.Span);
        Assert.Contains("\"AudioFileId\":", json);
        var deserialized = JsonSerializer.Deserialize<AudioProcessedMessage>(body.Span);
        Assert.NotNull(deserialized);
        Assert.Equal(audioFileId, deserialized!.AudioFileId);
    }

    [Fact]
    public void AudioProcessedMessage_RoundTripsThroughJson_WithEqualFields()
    {
        var audioFileId = Guid.NewGuid();
        var original = new AudioProcessedMessage(audioFileId);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(original);
        var roundTripped = JsonSerializer.Deserialize<AudioProcessedMessage>(bytes);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, roundTripped);
        Assert.Equal(audioFileId, roundTripped!.AudioFileId);
    }
}
