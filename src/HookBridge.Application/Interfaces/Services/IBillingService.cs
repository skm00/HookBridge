using HookBridge.Application.DTOs.Billing;

namespace HookBridge.Application.Interfaces.Services;

public interface IBillingService
{
    Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(
        string tenantId,
        CreateCheckoutSessionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<BillingStatusResponseDto?> GetBillingStatusAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task HandleStripeWebhookAsync(
        string jsonPayload,
        string stripeSignature,
        CancellationToken cancellationToken = default);
}
