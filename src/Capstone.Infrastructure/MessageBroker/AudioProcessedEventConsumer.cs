using System.Text.Json;
using Capstone.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Capstone.Infrastructure.MessageBroker;

public class AudioProcessedEventConsumer : BackgroundService
{
    private const int PollIntervalMs = 500;

    private readonly ILogger<AudioProcessedEventConsumer> _logger;
    private readonly IOptions<RabbitMQSettings> _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _stopLock = new();
    private bool _stopRequested;
    private IConnection? _connection;
    private IServiceScope? _serviceScope;

    public AudioProcessedEventConsumer(
        ILogger<AudioProcessedEventConsumer> logger,
        IOptions<RabbitMQSettings> settings,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _settings = settings;
        _scopeFactory = scopeFactory;
    }

    private bool StopRequested { get { lock (_stopLock) { return _stopRequested; } } set { lock (_stopLock) { _stopRequested = value; } } }
    private IConnection? Connection { get { lock (_stopLock) { return _connection; } } set { lock (_stopLock) { _connection = value; } } }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var settings = _settings.Value;

            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.Username,
                Password = settings.Password
            };

            var connection = await factory.CreateConnectionAsync();
            Connection = connection;

            await using (var setupChannel = await connection.CreateChannelAsync())
                await DeclareTopologyAsync(setupChannel, settings);

            var scope = _scopeFactory.CreateScope();
            _serviceScope = scope;
            var repository = scope.ServiceProvider.GetRequiredService<IAudioFileRepository>();

            var channel = await connection.CreateChannelAsync();

            _logger.LogInformation(
                "AudioProcessedEventConsumer started: exchange='{Exchange}', queue='{Queue}', routingKey='{RoutingKey}'",
                settings.ExchangeName, settings.ProcessedQueueName, settings.ProcessedRoutingKey);

            while (!StopRequested)
            {
                try
                {
                    var result = await channel.BasicGetAsync(settings.ProcessedQueueName, autoAck: false);
                    if (result is null)
                    {
                        await Task.Delay(PollIntervalMs);
                        continue;
                    }

                    await ProcessMessageAsync(channel, repository, result);
                }
                catch (Exception ex)
                {
                    if (StopRequested)
                        break;

                    _logger.LogError(ex, "Audio processed consumer channel error; recreating channel in {DelayMs}ms", PollIntervalMs);

                    try { await channel.DisposeAsync(); }
                    catch (Exception disposeEx) { _logger.LogDebug(disposeEx, "Consumer channel dispose failed"); }

                    await Task.Delay(PollIntervalMs);

                    try
                    {
                        channel = await CreateConsumerChannelAsync();
                    }
                    catch (Exception createEx)
                    {
                        if (StopRequested)
                            break;
                        _logger.LogError(createEx, "Could not recreate consumer channel; retrying in {DelayMs}ms", PollIntervalMs);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "AudioProcessedEventConsumer failed to start");
            StopRequested = true;
            await CloseConnectionAsync();
            DisposeScope();
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AudioProcessedEventConsumer stopping...");
        StopRequested = true;
        await CloseConnectionAsync();
        DisposeScope();
        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(IChannel channel, IAudioFileRepository repository, BasicGetResult result)
    {
        var deliveryTag = result.DeliveryTag;

        AudioProcessedEvent? message;
        try
        {
            message = JsonSerializer.Deserialize<AudioProcessedEvent>(result.Body.Span);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Received a malformed audio.processed event (delivery tag {DeliveryTag}); discarding without requeue", deliveryTag);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Received a null audio.processed event (delivery tag {DeliveryTag}); discarding without requeue", deliveryTag);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            if (await repository.MarkProcessedAsync(message.AudioFileId, DateTime.UtcNow))
            {
                _logger.LogInformation("Audio file {AudioFileId} marked as processed", message.AudioFileId);
            }
            else
            {
                _logger.LogWarning("audio.processed event for unknown audio file {AudioFileId}; acking anyway", message.AudioFileId);
            }

            await channel.BasicAckAsync(deliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark audio file {AudioFileId} as processed; discarding without requeue", message.AudioFileId);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel, RabbitMQSettings settings)
    {
        await channel.ExchangeDeclareAsync(
            exchange: settings.ExchangeName,
            type: settings.ExchangeType,
            durable: true,
            autoDelete: false);

        var queueDeclareOk = await channel.QueueDeclareAsync(
            queue: settings.ProcessedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        await channel.QueueBindAsync(queueDeclareOk.QueueName, settings.ExchangeName, settings.ProcessedRoutingKey);

        _logger.LogInformation(
            "Declared exchange '{Exchange}' and queue '{Queue}' bound with routing key '{RoutingKey}'",
            settings.ExchangeName, queueDeclareOk.QueueName, settings.ProcessedRoutingKey);
    }

    private async Task<IChannel> CreateConsumerChannelAsync()
    {
        var connection = Connection;
        if (connection is null)
            throw new InvalidOperationException("Consumer connection is not available");

        return await connection.CreateChannelAsync();
    }

    private async Task CloseConnectionAsync()
    {
        IConnection? connection;
        lock (_stopLock)
        {
            connection = _connection;
            _connection = null;
        }
        if (connection is null)
            return;

        try
        {
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection close during shutdown failed (likely already closed)");
        }
    }

    private void DisposeScope()
    {
        IServiceScope? scope;
        lock (_stopLock)
        {
            scope = _serviceScope;
            _serviceScope = null;
        }
        if (scope is null)
            return;

        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Scope dispose during shutdown failed");
        }
    }
}
