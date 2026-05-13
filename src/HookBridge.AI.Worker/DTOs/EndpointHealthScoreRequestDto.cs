namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Deterministic input contract for webhook target endpoint health scoring.
/// </summary>
public sealed class EndpointHealthScoreRequestDto
{
    public string EndpointId { get; set; } = string.Empty;

    public string? SubscriptionId { get; set; }

    public string? CustomerId { get; set; }

    public string? CustomerIdType { get; set; }

    public string? TargetUrl { get; set; }

    public string? Environment { get; set; }

    public int TotalDeliveries { get; set; }

    public int SuccessfulDeliveries { get; set; }

    public int FailedDeliveries { get; set; }

    public int TimeoutCount { get; set; }

    public int RateLimitCount { get; set; }

    public int ClientErrorCount { get; set; }

    public int ServerErrorCount { get; set; }

    public int RetryCount { get; set; }

    public int DeadLetterCount { get; set; }

    public double AverageLatencyMs { get; set; }

    public double P95LatencyMs { get; set; }

    public int? LastFailureStatusCode { get; set; }

    public string? LastFailureReason { get; set; }

    public DateTime? LastSuccessfulDeliveryAtUtc { get; set; }

    public DateTime? LastFailedDeliveryAtUtc { get; set; }

    public DateTime EvaluationWindowFromUtc { get; set; }

    public DateTime EvaluationWindowToUtc { get; set; }
}
