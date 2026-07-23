using System.Text;
using System.Text.Json;
using Capstone.Application.Interfaces;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Capstone.Infrastructure.MessageBroker;

public class RabbitMQEventPublisher : IAudioEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly RabbitMQSettings _settings;
    private bool _disposed;

    public RabbitMQEventPublisher(IOptions<RabbitMQSettings> settings)
    {
        _settings = settings.Value;

        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: _settings.ExchangeType,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();
    }

    public async Task PublishAudioUploadedAsync(
        Guid audioFileId,
        string storageUrl,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var message = new AudioUploadedEvent
        {
            AudioFileId = audioFileId,
            StorageUrl = storageUrl,
            FileName = fileName
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await _channel.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: _settings.RoutingKey,
            mandatory: false,
            body: body,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _disposed = true;
    }

    private record AudioUploadedEvent
    {
        public Guid AudioFileId { get; init; }
        public string StorageUrl { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
    }
}
