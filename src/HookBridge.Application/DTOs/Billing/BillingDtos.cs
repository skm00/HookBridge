using HookBridge.Domain.Enums;

namespace HookBridge.Application.DTOs.Billing;

public sealed class CreateCheckoutSessionRequestDto
{
    public BillingPlan Plan { get; set; }
}

public sealed class CheckoutSessionResponseDto
{
    public string SessionId { get; set; } = string.Empty;

    public string CheckoutUrl { get; set; } = string.Empty;
}

public sealed class BillingStatusResponseDto
{
    public string TenantId { get; set; } = string.Empty;

    public BillingPlan Plan { get; set; }

    public int MonthlyEventLimit { get; set; }

    public string BillingStatus { get; set; } = string.Empty;

    public string? StripeCustomerId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }
}
