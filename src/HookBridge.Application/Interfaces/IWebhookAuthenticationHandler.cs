using HookBridge.Application.Models.Delivery;

namespace HookBridge.Application.Interfaces;

public interface IWebhookAuthenticationHandler
{
    Task ApplyAsync(
        HttpRequestMessage httpRequest,
        WebhookDeliveryRequest deliveryRequest,
        CancellationToken cancellationToken = default);
}
