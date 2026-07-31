using System.Text.Json;
using RabbitMQ.Client;
using Capstone.Transcriber.Services;

namespace Capstone.Transcriber.Workers;

public class AudioProcessingWorker : BackgroundService
{
    private const string QueueName = "audio.transcribe";

    private readonly ILogger<AudioProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _exchangeName;
    private readonly string _routingKey;

    private readonly object _stopLock = new();
    private bool _stopRequested;
    private IConnection? _connection;
    private IServiceScope? _serviceScope;
    private Task[] _workers = [];

    public AudioProcessingWorker(ILogger<AudioProcessingWorker> logger, IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "capstone.audio";
        _routingKey = configuration["RabbitMQ:RoutingKey"] ?? "audio.uploaded";
    }

    internal static int ResolveConsumerCount(IConfiguration configuration)
    {
        var raw = configuration["Transcriber:MaxConcurrency"];
        var parsed = int.TryParse(raw, out var count) ? count : 2;
        return Math.Max(1, parsed);
    }

    private bool StopRequested { get { lock (_stopLock) { return _stopRequested; } } set { lock (_stopLock) { _stopRequested = value; } } }
    private IConnection? Connection { get { lock (_stopLock) { return _connection; } } set { lock (_stopLock) { _connection = value; } } }
    private Task[] Workers { get { lock (_stopLock) { return _workers; } } set { lock (_stopLock) { _workers = value; } } }

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
            var connection = await CreateConnectionFactory().CreateConnectionAsync();
            Connection = connection;

            await using (var setupChannel = await connection.CreateChannelAsync())
                await DeclareTopologyAsync(setupChannel);

            var scope = _scopeFactory.CreateScope();
            _serviceScope = scope;
            var processor = scope.ServiceProvider.GetRequiredService<AudioProcessorService>();

            var concurrency = ResolveConsumerCount(_configuration);
            var pollIntervalMs = Math.Max(1, _configuration.GetValue<int?>("Transcriber:PollIntervalMs") ?? 500);
            var workers = new Task[concurrency];
            for (var i = 0; i < concurrency; i++)
            {
                var workerIndex = i;
                var channel = await connection.CreateChannelAsync();
                workers[i] = Task.Run(() => WorkerLoopAsync(workerIndex, channel, processor, pollIntervalMs));
            }
            Workers = workers;

            _logger.LogInformation("AudioProcessingWorker started: exchange='{Exchange}', queue='{Queue}', workers={Concurrency} (poll interval={PollIntervalMs}ms)",
                _exchangeName, QueueName, concurrency, pollIntervalMs);

            while (!StopRequested)
                await Task.Delay(500);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "AudioProcessingWorker failed to start workers");
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

        var workers = Workers;
        if (workers.Length > 0)
        {
            var timeoutSeconds = Math.Max(0, _configuration.GetValue<int?>("Transcriber:ShutdownTimeoutSeconds") ?? 15);
            var workersTask = Task.WhenAll(workers);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            if (await Task.WhenAny(workersTask, timeoutTask) == workersTask)
                _logger.LogInformation("All worker tasks drained and exited");
            else
                _logger.LogWarning("Shutdown drain timeout ({TimeoutSeconds}s) reached with workers still processing; unacked messages will be requeued by the broker", timeoutSeconds);
        }

        DisposeScope();
        await base.StopAsync(cancellationToken);
    }

    private async Task WorkerLoopAsync(int workerIndex, IChannel channel, AudioProcessorService processor, int pollIntervalMs)
    {
        while (!StopRequested)
        {
            try
            {
                while (!StopRequested)
                    await PollAndProcessAsync(workerIndex, channel, processor, pollIntervalMs);
            }
            catch (Exception ex)
            {
                if (StopRequested)
                {
                    _logger.LogDebug(ex, "Worker {Worker} exiting: shutdown requested", workerIndex);
                    return;
                }
                _logger.LogError(ex, "Worker {Worker} channel error; discarding it and retrying", workerIndex);
            }
            finally
            {
                try { await channel.DisposeAsync(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Worker {Worker} channel dispose failed", workerIndex); }
            }

            if (StopRequested)
                return;

            await Task.Delay(pollIntervalMs);
            while (!StopRequested)
            {
                try
                {
                    channel = await CreateWorkerChannelAsync(workerIndex);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {Worker} could not recreate its channel; retrying in {DelayMs}ms", workerIndex, pollIntervalMs);
                    await Task.Delay(pollIntervalMs);
                }
            }
        }
    }

    private async Task PollAndProcessAsync(int workerIndex, IChannel channel, AudioProcessorService processor, int pollIntervalMs)
    {
        while (!StopRequested)
        {
            var result = await channel.BasicGetAsync(QueueName, autoAck: false);
            if (result is null)
            {
                await Task.Delay(pollIntervalMs);
                continue;
            }
            await ProcessMessageAsync(workerIndex, channel, processor, result);
        }
    }

    private async Task ProcessMessageAsync(int workerIndex, IChannel channel, AudioProcessorService processor, BasicGetResult result)
    {
        var deliveryTag = result.DeliveryTag;
        AudioUploadedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<AudioUploadedMessage>(result.Body.Span);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Worker {Worker} received a malformed message (delivery tag {DeliveryTag}); discarding without requeue", workerIndex, deliveryTag);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Worker {Worker} received a null message (delivery tag {DeliveryTag}); discarding without requeue", workerIndex, deliveryTag);
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
            return;
        }

        _logger.LogInformation("Worker {Worker} received event: AudioFileId={AudioFileId}, FileName={FileName}", workerIndex, message.AudioFileId, message.FileName);

        try
        {
            await processor.ProcessAsync(message);
            await channel.BasicAckAsync(deliveryTag, multiple: false);
            _logger.LogInformation("Worker {Worker} successfully processed AudioFileId={AudioFileId}", workerIndex, message.AudioFileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker {Worker} failed to process AudioFileId={AudioFileId}; discarding without requeue", workerIndex, message.AudioFileId);
            try
            {
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "Worker {Worker} could not nack delivery tag {DeliveryTag}; the broker will requeue it when the channel closes", workerIndex, deliveryTag);
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        var queueDeclareOk = await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueDeclareOk.QueueName, _exchangeName, _routingKey);
        _logger.LogInformation("Worker declared exchange '{Exchange}' and queue '{Queue}' bound with routing key '{RoutingKey}'", _exchangeName, queueDeclareOk.QueueName, _routingKey);
    }

    private async Task<IChannel> CreateWorkerChannelAsync(int workerIndex)
    {
        var connection = Connection;
        if (connection is null)
            throw new InvalidOperationException("Worker connection is not available");

        var channel = await connection.CreateChannelAsync();
        _logger.LogDebug("Worker {Worker} opened its private channel", workerIndex);
        return channel;
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
