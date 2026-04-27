using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Configuration;

/// <summary>
/// Default monthly event limits for each billing plan.
/// </summary>
public static class BillingPlanLimits
{
    public const int Free = 1_000;
    public const int Starter = 50_000;
    public const int Pro = 500_000;
    public const int Enterprise = int.MaxValue;

    public static int GetMonthlyLimit(BillingPlan plan) => plan switch
    {
        BillingPlan.Free => Free,
        BillingPlan.Starter => Starter,
        BillingPlan.Pro => Pro,
        BillingPlan.Enterprise => Enterprise,
        _ => Free,
    };
}
