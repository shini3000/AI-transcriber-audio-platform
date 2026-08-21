using System.Runtime.CompilerServices;
using System.Text.Json;
using Capstone.Transcriber.Messaging;
using Capstone.Transcriber.Services;
using RabbitMQ.Client;

namespace Capstone.Transcriber.Workers;

public class AudioProcessingWorker : BackgroundService
{
    private const string QueueName = "audio.transcribe";

    private readonly ILogger<AudioProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioProcessedPublisher _processedPublisher;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly string _processedRoutingKey;

    private readonly object _stopLock = new();
    private bool _stopRequested;
    private IConnection? _connection;
    private IServiceScope? _serviceScope;

    public AudioProcessingWorker(
        ILogger<AudioProcessingWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        AudioProcessedPublisher processedPublisher)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _processedPublisher = processedPublisher;
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "capstone.audio";
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "audio.compressed";
        _processedRoutingKey = configuration["RabbitMQ:ProcessedRoutingKey"] ?? "audio.processed";
    }

    internal static int ResolveConsumerCount(IConfiguration configuration)
    {
        var raw = configuration["Transcriber:MaxConcurrency"];
        var parsed = int.TryParse(raw, out var count) ? count : 2;
        return Math.Max(1, parsed);
    }

    private bool StopRequested { get { lock (_stopLock) { return _stopRequested; } } set { lock (_stopLock) { _stopRequested = value; } } }
    private IConnection? Connection { get { lock (_stopLock) { return _connection; } } set { lock (_stopLock) { _connection = value; } } }

    private ConnectionFactory CreateConnectionFactory() => new()
    {
        HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
        Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = _configuration["RabbitMQ:Username"] ?? "guest",
        Password = _configuration["RabbitMQ:Password"] ?? "guest"
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connection = await CreateConnectionFactory().CreateConnectionAsync(stoppingToken);
            Connection = connection;

            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await DeclareTopologyAsync(channel);

            var scope = _scopeFactory.CreateScope();
            _serviceScope = scope;
            var processor = scope.ServiceProvider.GetRequiredService<AudioProcessorService>();

            var concurrency = ResolveConsumerCount(_configuration);
            var pollIntervalMs = Math.Max(1, _configuration.GetValue<int?>("Transcriber:PollIntervalMs") ?? 500);

            _logger.LogInformation(
                "AudioProcessingWorker started with Parallel.ForEachAsync pool: exchange='{Exchange}', queue='{Queue}', concurrency={Concurrency}",
                _exchangeName, QueueName, concurrency);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = concurrency,
                CancellationToken = stoppingToken
            };

            await Parallel.ForEachAsync(
                GetMessageStreamAsync(channel, QueueName, pollIntervalMs, stoppingToken),
                parallelOptions,
                async (result, ct) =>
                {
                    await ProcessMessageAsync(channel, processor, result, ct);
                });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("AudioProcessingWorker stopping cancellation received");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "AudioProcessingWorker encountered a fatal error");
            StopRequested = true;
            await CloseConnectionAsync();
            DisposeScope();
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AudioProcessingWorker stopping...");
        StopRequested = true;
        await CloseConnectionAsync();
        DisposeScope();
        await base.StopAsync(cancellationToken);
    }

    private async IAsyncEnumerable<BasicGetResult> GetMessageStreamAsync(
        IChannel channel,
        string queueName,
        int pollIntervalMs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !StopRequested)
        {
            BasicGetResult? result;
            try
            {
                result = await channel.BasicGetAsync(queueName, autoAck: false, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling message from RabbitMQ in Transcriber; retrying in {DelayMs}ms", pollIntervalMs);
                await Task.Delay(pollIntervalMs, cancellationToken);
                continue;
            }

            if (result is null)
            {
                await Task.Delay(pollIntervalMs, cancellationToken);
                continue;
            }

            yield return result;
        }
    }

    private async Task ProcessMessageAsync(
        IChannel channel,
        AudioProcessorService processor,
        BasicGetResult result,
        CancellationToken cancellationToken)
    {
        var deliveryTag = result.DeliveryTag;
        AudioUploadedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<AudioUploadedMessage>(result.Body.Span);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Transcriber received a malformed message; discarding without requeue");
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Transcriber received a null message; discarding without requeue");
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        _logger.LogInformation("Transcriber processing AudioFileId={AudioFileId}, FileName={FileName}",
            message.AudioFileId, message.FileName);

        try
        {
            await processor.ProcessAsync(message);

            try
            {
                await _processedPublisher.PublishAsync(channel, message.AudioFileId, _processedRoutingKey, _exchangeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transcriber could not publish processed event for AudioFileId={AudioFileId}; continuing with ack", message.AudioFileId);
            }

            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
            _logger.LogInformation("Transcriber successfully processed and acked AudioFileId={AudioFileId}", message.AudioFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcriber failed to process AudioFileId={AudioFileId}; discarding without requeue", message.AudioFileId);
            try
            {
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "Transcriber could not nack delivery tag");
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        var queueDeclareOk = await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueDeclareOk.QueueName, _exchangeName, _routingKey);
        _logger.LogInformation("Transcriber declared exchange '{Exchange}' and queue '{Queue}' bound with routing key '{RoutingKey}'",
            _exchangeName, queueDeclareOk.QueueName, _routingKey);
    }

    private async Task CloseConnectionAsync()
    {
        IConnection? connection;
        lock (_stopLock)
        {
            connection = _connection;
            _connection = null;
        }
        if (connection is null) return;

        try
        {
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection close during shutdown failed");
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
        if (scope is null) return;

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
