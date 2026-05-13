using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "Ollama";

    public string Model { get; set; } = "llama3";

    public string Endpoint { get; set; } = "http://localhost:11434";

    [Range(1, int.MaxValue, ErrorMessage = "AI:TimeoutSeconds must be greater than 0.")]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(0, int.MaxValue, ErrorMessage = "AI:MaxRetries must be 0 or greater.")]
    public int MaxRetries { get; set; } = 3;

    public string SystemPrompt { get; set; } =
        "You are HookBridge AI, an assistant for webhook failure analysis and event processing.";

    public bool EnablePromptLogging { get; set; }

    public bool EnableFallback { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "AI:LlmRequestTimeoutSeconds must be greater than 0.")]
    public int LlmRequestTimeoutSeconds { get; set; } = 30;

    [Range(1, int.MaxValue, ErrorMessage = "AI:MaxFallbackSummaryLength must be greater than 0.")]
    public int MaxFallbackSummaryLength { get; set; } = 1000;

    public string HealthCheckPrompt { get; set; } = "Say HookBridge AI is ready";

    [Range(1, int.MaxValue, ErrorMessage = "AI:MaxPromptPayloadLength must be greater than 0.")]
    public int MaxPromptPayloadLength { get; set; } = 4000;

    public bool MaskSensitiveValues { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "AI:MaxLogEntriesForSummary must be greater than 0.")]
    public int MaxLogEntriesForSummary { get; set; } = 100;

    [Range(1, int.MaxValue, ErrorMessage = "AI:MaxLogMessageLength must be greater than 0.")]
    public int MaxLogMessageLength { get; set; } = 2000;
}
