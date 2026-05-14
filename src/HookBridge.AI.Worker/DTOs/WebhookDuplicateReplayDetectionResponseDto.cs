namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookDuplicateReplayDetectionResponseDto
{
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsReplay { get; set; }
    public WebhookDuplicateReplayReason DuplicateReason { get; set; }
    public WebhookDuplicateReplayReason ReplayReason { get; set; }
    public string? PayloadHash { get; set; }
    public string? SignatureHash { get; set; }
    public int DetectionScore { get; set; }
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public WebhookDuplicateReplaySuggestedAction SuggestedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
}
