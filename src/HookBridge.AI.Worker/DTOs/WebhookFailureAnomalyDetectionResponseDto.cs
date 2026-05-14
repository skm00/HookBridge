namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookFailureAnomalyDetectionResponseDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? TargetUrl { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public bool IsAnomalyDetected { get; set; }
    public int AnomalyScore { get; set; }
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<WebhookFailureAnomalyDto> DetectedAnomalies { get; set; } = [];
    public DateTime CalculatedAtUtc { get; set; }
}
