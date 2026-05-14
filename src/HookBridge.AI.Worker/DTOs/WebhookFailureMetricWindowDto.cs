namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookFailureMetricWindowDto
{
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public int TotalDeliveries { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public int RetryCount { get; set; }
    public int DeadLetterCount { get; set; }
    public int TimeoutCount { get; set; }
    public int RateLimitCount { get; set; }
    public int ClientErrorCount { get; set; }
    public int ServerErrorCount { get; set; }
    public int AuthenticationFailureCount { get; set; }
    public int SignatureValidationFailureCount { get; set; }
    public int SuspiciousPayloadCount { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
}
