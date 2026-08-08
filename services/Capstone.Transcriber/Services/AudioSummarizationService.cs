using LLama;
using LLama.Common;
using LLama.Abstractions;
using LLama.Sampling;

namespace Capstone.Transcriber.Services;

public class AudioSummarizationService
{
    private readonly ILogger<AudioSummarizationService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly TranscriptionModels _models;

    public AudioSummarizationService(
        ILogger<AudioSummarizationService> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        TranscriptionModels models)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _models = models;
    }

    public async Task<string> GenerateSummaryAsync(string text)
    {
        var modelPath = _configuration["Model:QwenPath"]
            ?? throw new InvalidOperationException("Model:QwenPath not configured");

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Qwen model not found at {Path}. Using first sentence as fallback.", modelPath);
            return GetFallbackSummary(text);
        }

        try
        {
            _logger.LogDebug("Creating per-message Qwen context from shared weights ({Path})...", modelPath);

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 1024,
                GpuLayerCount = 0
            };

            using var context = _models.GetQwenWeights().CreateContext(parameters, _loggerFactory.CreateLogger<LLamaContext>());
            var executor = new InteractiveExecutor(context, _loggerFactory.CreateLogger<InteractiveExecutor>());

            _logger.LogDebug("Running inference for summary generation...");

            var prompt = $@"<|system|>
summarize:

<|user|>
{text}

<|assistant|>";

            var result = string.Empty;
            var inferenceParams = new InferenceParams
            {
                MaxTokens = 64,
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.3f }
            };

            await foreach (var token in executor.InferAsync(prompt, inferenceParams))
            {
                result += token;
                if (result.Contains('\n') || result.Length > 200)
                    break;
            }

            return result.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLamaSharp failed, using fallback summary");
            return GetFallbackSummary(text);
        }
    }

    private static string GetFallbackSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Untitled";

        var cleaned = text.Trim();
        var endIdx = cleaned.IndexOfAny(['.', '!', '?', '\n']);
        var firstSentence = endIdx > 0 ? cleaned[..endIdx].Trim() : cleaned;

        if (firstSentence.Length > 60)
        {
            var truncated = firstSentence[..60];
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > 30)
            {
                return truncated[..lastSpace].Trim() + "...";
            }
            return truncated.Trim() + "...";
        }

        return firstSentence;
    }
}
