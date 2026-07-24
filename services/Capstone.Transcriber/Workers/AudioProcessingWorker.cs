using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Capstone.Transcriber;
using Capstone.Transcriber.Services;

namespace Capstone.Transcriber.Workers;

public class AudioProcessingWorker : BackgroundService
{
    private readonly ILogger<AudioProcessingWorker> _logger;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly IServiceScopeFactory _scopeFactory;

    public AudioProcessingWorker(
        ILogger<AudioProcessingWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "capstone.audio";
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "audio.uploaded";

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        _channel.ExchangeDeclareAsync(
            exchange: _exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false).GetAwaiter().GetResult();

        var queueName = _channel.QueueDeclareAsync(
            queue: "audio.transcribe",
            durable: true,
            exclusive: false,
            autoDelete: false).GetAwaiter().GetResult().QueueName;

        _channel.QueueBindAsync(
            queue: queueName,
            exchange: _exchangeName,
            routingKey: _routingKey).GetAwaiter().GetResult();

        _logger.LogInformation("Worker connected to RabbitMQ exchange '{Exchange}' with queue '{Queue}'",
            _exchangeName, queueName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AudioProcessingWorker started, waiting for messages...");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = JsonSerializer.Deserialize<AudioUploadedMessage>(body);

            if (message is null)
            {
                _logger.LogWarning("Received null message, skipping");
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, false);
                return;
            }

            _logger.LogInformation(
                "Received event: AudioFileId={AudioFileId}, FileName={FileName}",
                message.AudioFileId, message.FileName);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AudioProcessorService>();

                await processor.ProcessAsync(message, stoppingToken);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                _logger.LogInformation("Successfully processed AudioFileId={AudioFileId}", message.AudioFileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process AudioFileId={AudioFileId}", message.AudioFileId);
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, true); // requeue
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "audio.transcribe",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _channel.CloseAsync(cancellationToken);
        await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

}
