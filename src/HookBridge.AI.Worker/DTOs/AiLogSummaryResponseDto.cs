namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// AI-generated webhook log summary output for debugging and support workflows.
/// </summary>
public sealed class AiLogSummaryResponseDto
{
    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string RootCause { get; set; } = string.Empty;

    public string Impact { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;

    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;

    public double ConfidenceScore { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public string Model { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public AiFallbackMetadataDto? Fallback { get; set; }

    public string PromptName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;
}
