using HookBridge.Application.Models.Delivery;

namespace HookBridge.Application.Interfaces;

public interface IWebhookDeliveryClient
{
    Task<WebhookDeliveryResult> SendAsync(
        WebhookDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
