namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Clean input contract for AI-powered webhook delivery failure analysis.
/// </summary>
public sealed class WebhookFailureAnalysisRequestDto
{
    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string? SubscriptionId { get; set; }

    public string? CustomerId { get; set; }

    public string? CustomerIdType { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Source { get; set; }

    public string? TargetUrl { get; set; }

    public string? HttpMethod { get; set; }

    public int? StatusCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? FailureReason { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; }

    public Dictionary<string, string>? RequestHeaders { get; set; }

    public Dictionary<string, string>? ResponseHeaders { get; set; }

    public string? RequestPayload { get; set; }

    public string? ResponseBody { get; set; }

    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
}
