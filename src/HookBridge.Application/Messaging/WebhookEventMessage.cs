namespace HookBridge.Application.Messaging;

public sealed class WebhookEventMessage
{
    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public string? CorrelationId { get; set; }
}
