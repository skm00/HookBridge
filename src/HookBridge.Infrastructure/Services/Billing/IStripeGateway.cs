using Stripe;
using Stripe.Checkout;

namespace HookBridge.Infrastructure.Services.Billing;

public interface IStripeGateway
{
    Task<Customer> CreateCustomerAsync(CustomerCreateOptions options, CancellationToken cancellationToken = default);

    Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, CancellationToken cancellationToken = default);

    Event ConstructWebhookEvent(string payload, string signatureHeader, string webhookSecret);
}
