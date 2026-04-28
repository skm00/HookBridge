using HookBridge.Domain.Enums;
using HookBridge.Domain.Configuration;

namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents a customer tenant in the HookBridge platform.
/// </summary>
public sealed class Tenant : BaseEntity
{
    /// <summary>
    /// Gets or sets the tenant billing plan.
    /// </summary>
    public BillingPlan Plan { get; set; } = BillingPlan.Free;

    /// <summary>
    /// Gets or sets the max number of events accepted in the current month.
    /// </summary>
    public int MonthlyEventLimit { get; set; } = BillingPlanLimits.Free;

    /// <summary>
    /// Gets or sets the Stripe customer identifier for this tenant.
    /// </summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe subscription identifier for this tenant.
    /// </summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the billing status for this tenant.
    /// </summary>
    public string BillingStatus { get; set; } = "Free";

    /// <summary>
    /// Gets or sets the start of the current billing period.
    /// </summary>
    public DateTime? CurrentPeriodStart { get; set; }

    /// <summary>
    /// Gets or sets the end of the current billing period.
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the tenant display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique tenant slug.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant status.
    /// </summary>
    public TenantStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the optional tenant contact email.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the notification destination emails for important alerts.
    /// </summary>
    public List<string> NotificationEmails { get; set; } = [];
}
