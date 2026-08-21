using System.Runtime.CompilerServices;
using System.Text.Json;
using Capstone.Validator.Messaging;
using Capstone.Validator.Services;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace Capstone.Validator.Workers;

public class AudioValidationWorker : BackgroundService
{
    private const string QueueName = "audio.validate";

    private readonly ILogger<AudioValidationWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AudioValidatedPublisher _publisher;
    private readonly string _exchangeName;
    private readonly string _incomingRoutingKey;
    private readonly string _validatedRoutingKey;

    private readonly object _stopLock = new();
    private bool _stopRequested;
    private IConnection? _connection;

    public AudioValidationWorker(
        ILogger<AudioValidationWorker> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        AudioValidatedPublisher publisher)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "capstone.audio";
        _incomingRoutingKey = configuration["RabbitMQ:RoutingKey"] ?? "audio.uploaded";
        _validatedRoutingKey = configuration["RabbitMQ:ValidatedRoutingKey"] ?? "audio.validated";
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

            var maxConcurrency = Math.Max(1, _configuration.GetValue<int?>("Validator:MaxConcurrency") ?? 4);
            var pollIntervalMs = Math.Max(1, _configuration.GetValue<int?>("Validator:PollIntervalMs") ?? 500);

            _logger.LogInformation(
                "AudioValidationWorker started: exchange='{Exchange}', queue='{Queue}', concurrency={Concurrency}",
                _exchangeName, QueueName, maxConcurrency);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = stoppingToken
            };

            await Parallel.ForEachAsync(
                GetMessageStreamAsync(channel, QueueName, pollIntervalMs, stoppingToken),
                parallelOptions,
                async (result, ct) => await ProcessMessageAsync(channel, result, ct));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("AudioValidationWorker stopping: cancellation received");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "AudioValidationWorker encountered a fatal error");
            StopRequested = true;
            await CloseConnectionAsync();
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("AudioValidationWorker stopping...");
        StopRequested = true;
        await CloseConnectionAsync();
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
                _logger.LogError(ex, "Error polling from RabbitMQ; retrying in {DelayMs}ms", pollIntervalMs);
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
            _logger.LogWarning("Received null message; discarding");
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        _logger.LogInformation("Validating AudioFileId={AudioFileId}, FileName={FileName}",
            message.AudioFileId, message.FileName);

        var tempPath = Path.Combine(Path.GetTempPath(), $"validate_{Guid.NewGuid()}_{message.FileName}");
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var downloader = scope.ServiceProvider.GetRequiredService<AudioFileDownloader>();
            var validator = scope.ServiceProvider.GetRequiredService<AudioMagicBytesValidator>();

            var audioFile = await db.AudioFiles.FirstOrDefaultAsync(f => f.Id == message.AudioFileId, cancellationToken);
            if (audioFile is null)
            {
                _logger.LogWarning("AudioFileId={AudioFileId} not found in database; discarding", message.AudioFileId);
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
                return;
            }

            try
            {
                await downloader.DownloadFromMinioAsync(message.StorageUrl, tempPath);
            }
            catch (Exception dlEx)
            {
                _logger.LogError(dlEx, "Failed to download file from storage for AudioFileId={AudioFileId}", message.AudioFileId);
                audioFile.Status = "rejected";
                audioFile.ValidationErrorMessage = "Failed to retrieve file from storage for validation.";
                audioFile.ValidatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
                return;
            }

            var validation = await validator.ValidateAsync(tempPath, audioFile.ContentType);

            if (validation.IsValid)
            {
                audioFile.Status = "validated";
                audioFile.ValidatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                await _publisher.PublishAsync(channel, message.AudioFileId, message.StorageUrl, message.FileName,
                    _validatedRoutingKey, _exchangeName);

                _logger.LogInformation("AudioFileId={AudioFileId} validated OK — published audio.validated", message.AudioFileId);
            }
            else
            {
                audioFile.Status = "rejected";
                audioFile.ValidationErrorMessage = validation.ErrorMessage;
                audioFile.ValidatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("AudioFileId={AudioFileId} rejected: {Reason}", message.AudioFileId, validation.ErrorMessage);
            }

            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing AudioFileId={AudioFileId}", message.AudioFileId);
            try
            {
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
            catch (Exception nackEx)
            {
                _logger.LogWarning(nackEx, "Could not nack delivery tag");
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        var queueDeclareOk = await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queueDeclareOk.QueueName, _exchangeName, _incomingRoutingKey);
        _logger.LogInformation(
            "Declared exchange '{Exchange}' and queue '{Queue}' bound with routing key '{RoutingKey}'",
            _exchangeName, queueDeclareOk.QueueName, _incomingRoutingKey);
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
}
