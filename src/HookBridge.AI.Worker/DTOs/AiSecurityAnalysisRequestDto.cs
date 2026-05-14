namespace HookBridge.AI.Worker.DTOs;

public sealed class AiSecurityAnalysisRequestDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? Source { get; set; }
    public string? EventType { get; set; }
    public string? TargetUrl { get; set; }
    public string? HttpMethod { get; set; }
    public IDictionary<string, string>? Headers { get; set; }
    public object? Payload { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public bool SignatureValidationFailed { get; set; }
    public bool AuthenticationFailed { get; set; }
    public long PayloadSizeBytes { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
