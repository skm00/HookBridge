namespace HookBridge.Application.DTOs.AiAnalysis;

/// <summary>
/// Response returned for a stored AI-generated webhook failure analysis result.
/// </summary>
public sealed class AiAnalysisResultResponseDto
{
    public string? Id { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string Source { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string AiSummary { get; set; } = string.Empty;

    public string RootCause { get; set; } = string.Empty;

    public string AiRecommendation { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    public double ConfidenceScore { get; set; }

    public string SuggestedRetryAction { get; set; } = string.Empty;

    public bool IsRetryRecommended { get; set; }

    public string Model { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string PromptName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
