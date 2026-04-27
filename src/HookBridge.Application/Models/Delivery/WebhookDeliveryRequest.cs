using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Models.Delivery;

public sealed class WebhookDeliveryRequest
{
    public string TargetUrl { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public object Payload { get; set; } = new();

    public List<KeyValueDto> Headers { get; set; } = [];

    public int TimeoutSeconds { get; set; }

    public string? CorrelationId { get; set; }
}
