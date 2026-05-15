namespace HookBridge.AI.Worker.DTOs;

public sealed class AiSecurityAnalysisResponseDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public bool IsSuspicious { get; set; }
    public int SecurityRiskScore { get; set; }
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<AiSecuritySignalDto> DetectedSecuritySignals { get; set; } = Array.Empty<AiSecuritySignalDto>();
    public AiSecuritySuggestedAction SuggestedAction { get; set; } = AiSecuritySuggestedAction.None;
    public double ConfidenceScore { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public AiFallbackMetadataDto? Fallback { get; set; }

    public string PromptName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;
}
