using System.Runtime.CompilerServices;
using System.Text.Json;
using Capstone.Compressor.Messaging;
using Capstone.Compressor.Services;
using RabbitMQ.Client;

namespace Capstone.Compressor.Workers;

public class AudioCompressionWorker : BackgroundService
{
    private const string QueueName = "audio.compress";

    private readonly ILogger<AudioCompressionWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioCompressedPublisher _publisher;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly string _compressedRoutingKey;

    private readonly object _stopLock = new();
    private bool _stopRequested;
    private IConnection? _connection;
    private IServiceScope? _serviceScope;

    public AudioCompressionWorker(
        ILogger<AudioCompressionWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        AudioCompressedPublisher publisher)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "capstone.audio";
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "audio.uploaded";
        _compressedRoutingKey = configuration["RabbitMQ:CompressedRoutingKey"] ?? "audio.compressed";
    }

    internal static int ResolveConsumerCount(IConfiguration configuration)
    {
        var raw = configuration["Compressor:MaxConcurrency"];
        var parsed = int.TryParse(raw, out var count) ? count : 4;
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
            var processor = scope.ServiceProvider.GetRequiredService<AudioCompressionProcessor>();

            var concurrency = ResolveConsumerCount(_configuration);
            var pollIntervalMs = Math.Max(1, _configuration.GetValue<int?>("Compressor:PollIntervalMs") ?? 500);

            _logger.LogInformation(
                "AudioCompressionWorker started with Parallel.ForEachAsync pool: exchange='{Exchange}', queue='{Queue}', concurrency={Concurrency}",
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
            _logger.LogInformation("AudioCompressionWorker stopping cancellation received");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "AudioCompressionWorker encountered a fatal error");
            StopRequested = true;
            await CloseConnectionAsync();
            DisposeScope();
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AudioCompressionWorker stopping...");
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
                _logger.LogError(ex, "Error polling message from RabbitMQ; retrying in {DelayMs}ms", pollIntervalMs);
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
        AudioCompressionProcessor processor,
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
            _logger.LogError(ex, "Received malformed message; discarding without requeue");
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Received null message; discarding without requeue");
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        _logger.LogInformation("Processing audio.uploaded message: AudioFileId={AudioFileId}, FileName={FileName}",
            message.AudioFileId, message.FileName);

        try
        {
            var compressedUrl = await processor.ProcessAsync(message, cancellationToken);
            var compressedFileName = Path.ChangeExtension(message.FileName, ".aac");

            try
            {
                await _publisher.PublishAsync(channel, message.AudioFileId, compressedUrl, compressedFileName, _compressedRoutingKey, _exchangeName);
                _logger.LogInformation("Published audio.compressed event for AudioFileId={AudioFileId}", message.AudioFileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish audio.compressed for AudioFileId={AudioFileId}", message.AudioFileId);
            }

            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
            _logger.LogInformation("Successfully compressed and acked AudioFileId={AudioFileId}", message.AudioFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process compression for AudioFileId={AudioFileId}", message.AudioFileId);
            try
            {
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "Could not nack delivery tag");
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        var queueDeclareOk = await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueDeclareOk.QueueName, _exchangeName, _routingKey);
        _logger.LogInformation("Declared exchange '{Exchange}' and queue '{Queue}' bound with routing key '{RoutingKey}'",
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
