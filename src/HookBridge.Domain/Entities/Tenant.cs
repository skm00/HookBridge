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
}
