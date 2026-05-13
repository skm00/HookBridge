namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// AI-generated webhook delivery failure analysis output.
/// </summary>
public sealed class WebhookFailureAnalysisResponseDto
{
    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string AiSummary { get; set; } = string.Empty;

    public string RootCause { get; set; } = string.Empty;

    public string AiRecommendation { get; set; } = string.Empty;

    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;

    public double ConfidenceScore { get; set; }

    public SuggestedRetryAction SuggestedRetryAction { get; set; } = SuggestedRetryAction.None;

    public bool IsRetryRecommended { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public string Model { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
}
