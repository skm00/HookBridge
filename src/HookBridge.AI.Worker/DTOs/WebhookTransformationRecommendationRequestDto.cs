namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookTransformationRecommendationRequestDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public object? SourcePayload { get; set; }
    public object? TargetSchema { get; set; }
    public object? TargetSamplePayload { get; set; }
    public object? ExistingMappingRules { get; set; }
    public string? TargetUrl { get; set; }
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public DateTime ReceivedAtUtc { get; set; }
}
