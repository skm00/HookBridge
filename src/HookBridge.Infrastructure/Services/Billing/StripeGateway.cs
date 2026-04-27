using Stripe;
using Stripe.Checkout;

namespace HookBridge.Infrastructure.Services.Billing;

public sealed class StripeGateway : IStripeGateway
{
    private readonly CustomerService customerService = new();
    private readonly SessionService sessionService = new();

    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options, CancellationToken cancellationToken = default)
        => customerService.CreateAsync(options, cancellationToken: cancellationToken);

    public Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, CancellationToken cancellationToken = default)
        => sessionService.CreateAsync(options, cancellationToken: cancellationToken);

    public Event ConstructWebhookEvent(string payload, string signatureHeader, string webhookSecret)
        => EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);
}
