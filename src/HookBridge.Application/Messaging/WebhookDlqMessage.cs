namespace HookBridge.Application.Messaging;

public sealed class WebhookDlqMessage
{
    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public int FinalAttemptNumber { get; set; }

    public string? CorrelationId { get; set; }
}
