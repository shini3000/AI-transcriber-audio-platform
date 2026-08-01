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
            _logger.LogWarning("Qwen model not found at {Path}. Using first 50 chars as fallback.", modelPath);
            return TruncateToFirstSentence(text, 50);
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
You are a title generator. Given a conversation transcript, generate a short title (max 50 characters) that captures the main topic, like ChatGPT conversation titles.

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
                if (result.Contains('\n') || result.Length > 100)
                    break;
            }

            result = result.Trim().TrimEnd('.', '!', '?');
            return TruncateToFirstSentence(result, 50);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLamaSharp failed, using fallback truncation");
            return TruncateToFirstSentence(text, 50);
        }
    }

    private static string TruncateToFirstSentence(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Untitled";

        var cleaned = text.Trim();
        if (cleaned.Length <= maxLength)
            return cleaned;

        var endIdx = cleaned.IndexOfAny(['.', '!', '?', '\n']);
        if (endIdx > 0 && endIdx <= maxLength)
            return cleaned[..endIdx].Trim();

        return cleaned[..maxLength].Trim();
    }
}
