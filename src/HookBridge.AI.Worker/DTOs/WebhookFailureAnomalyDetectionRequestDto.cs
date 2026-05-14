namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookFailureAnomalyDetectionRequestDto
{
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? TargetUrl { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public WebhookFailureMetricWindowDto? CurrentWindow { get; set; }
    public WebhookFailureMetricWindowDto? BaselineWindow { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
