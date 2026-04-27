using HookBridge.Application.Messaging;

namespace HookBridge.Application.Interfaces.Services;

public interface IWebhookDeliveryService
{
    Task ProcessEventAsync(
        WebhookEventMessage message,
        CancellationToken cancellationToken = default);
}
