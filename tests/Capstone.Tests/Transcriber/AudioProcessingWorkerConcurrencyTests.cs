using Capstone.Transcriber.Workers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Capstone.Tests.Transcriber;

/// <summary>
/// Validates the worker's concurrency bound without coupling to RabbitMQ.
///
/// The worker starts exactly <c>Transcriber:MaxConcurrency</c> consumers, each on its own
/// channel with prefetch=1, so at most MaxConcurrency messages are processed concurrently.
/// These tests lock the resolution of that bound (default, override, and clamping).
///
/// A counter-based test that observes N consumers actually in flight is not viable here:
/// it would require a live RabbitMQ broker (or mocking the whole RabbitMQ.Client surface),
/// which is explicitly out of scope. The clamp tests below are the meaningful, mock-free
/// contract for the concurrency limit.
/// </summary>
public class AudioProcessingWorkerConcurrencyTests
{
    private static IConfiguration ConfigWithMaxConcurrency(string? value)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);
        if (value is not null)
        {
            configuration.Setup(c => c["Transcriber:MaxConcurrency"]).Returns(value);
        }
        return configuration.Object;
    }

    [Fact]
    public void ResolveConsumerCount_WhenNotConfigured_DefaultsToTwo()
    {
        Assert.Equal(2, AudioProcessingWorker.ResolveConsumerCount(ConfigWithMaxConcurrency(null)));
    }

    [Fact]
    public void ResolveConsumerCount_WhenConfigured_ReturnsConfiguredValue()
    {
        Assert.Equal(5, AudioProcessingWorker.ResolveConsumerCount(ConfigWithMaxConcurrency("5")));
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("-2", 1)]
    [InlineData("1", 1)]
    public void ResolveConsumerCount_IsClampedToAtLeastOne(string rawValue, int expected)
    {
        Assert.Equal(expected, AudioProcessingWorker.ResolveConsumerCount(ConfigWithMaxConcurrency(rawValue)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void ResolveConsumerCount_WhenValueIsNotNumeric_DefaultsToTwo(string rawValue)
    {
        Assert.Equal(2, AudioProcessingWorker.ResolveConsumerCount(ConfigWithMaxConcurrency(rawValue)));
    }
}
