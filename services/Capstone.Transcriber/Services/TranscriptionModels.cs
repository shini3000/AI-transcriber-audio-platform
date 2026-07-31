using LLama;
using LLama.Common;
using Whisper.net;

namespace Capstone.Transcriber.Services;

public sealed class TranscriptionModels : IDisposable
{
    private readonly ILogger<TranscriptionModels> _logger;
    private readonly IConfiguration _configuration;

    private readonly object _modelLock = new();
    private WhisperFactory? _whisperFactory;
    private LLamaWeights? _qwenWeights;
    private readonly SemaphoreSlim _whisperGate = new(1, 1);
    private bool _disposed;

    public TranscriptionModels(ILogger<TranscriptionModels> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public WhisperFactory GetWhisperFactory()
    {
        if (_whisperFactory is not null)
            return _whisperFactory;

        if (!Monitor.TryEnter(_modelLock, TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Timed out waiting for the Whisper model initialization lock");

        try
        {
            _whisperFactory ??= LoadWhisperFactory();
            return _whisperFactory;
        }
        finally
        {
            Monitor.Exit(_modelLock);
        }
    }

    public LLamaWeights GetQwenWeights()
    {
        if (_qwenWeights is not null)
            return _qwenWeights;

        if (!Monitor.TryEnter(_modelLock, TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Timed out waiting for the Qwen model initialization lock");

        try
        {
            _qwenWeights ??= LoadQwenWeights();
            return _qwenWeights;
        }
        finally
        {
            Monitor.Exit(_modelLock);
        }
    }

    public async Task<IDisposable> AcquireWhisperAsync()
    {
        if (!await _whisperGate.WaitAsync(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Timed out waiting for the Whisper model gate");
        return new WhisperLease(this);
    }

    private WhisperFactory LoadWhisperFactory()
    {
        var whisperModelPath = _configuration["Model:WhisperModelPath"] ?? "tiny";
        if (whisperModelPath != "tiny" && !File.Exists(whisperModelPath))
        {
            var absolutePath = Path.GetFullPath(whisperModelPath);
            throw new FileNotFoundException($"Whisper model file not found. Checked path: {absolutePath}");
        }

        _logger.LogInformation("Loading Whisper model from '{Path}' (once per process)", whisperModelPath);
        return WhisperFactory.FromPath(whisperModelPath);
    }

    private LLamaWeights LoadQwenWeights()
    {
        var modelPath = _configuration["Model:QwenPath"]
            ?? throw new InvalidOperationException("Model:QwenPath not configured");

        _logger.LogInformation("Loading Qwen weights from '{Path}' (once per process)", modelPath);

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024,
            GpuLayerCount = 0
        };

        return LLamaWeights.LoadFromFile(parameters);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _whisperFactory?.Dispose();
        _qwenWeights?.Dispose();
        _whisperGate.Dispose();
    }

    private sealed class WhisperLease : IDisposable
    {
        private readonly TranscriptionModels _owner;
        private bool _released;

        public WhisperLease(TranscriptionModels owner) => _owner = owner;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }
            _released = true;
            _owner._whisperGate.Release();
        }
    }
}
