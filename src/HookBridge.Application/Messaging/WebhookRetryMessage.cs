namespace HookBridge.Application.Messaging;

public sealed class WebhookRetryMessage
{
    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string? FailedEventId { get; set; }

    public int AttemptNumber { get; set; }

    public DateTime NextRetryAt { get; set; }

    public string? CorrelationId { get; set; }
}
