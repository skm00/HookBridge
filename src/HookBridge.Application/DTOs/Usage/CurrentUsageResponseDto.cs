using HookBridge.Domain.Enums;

namespace HookBridge.Application.DTOs.Usage;

public sealed class CurrentUsageResponseDto
{
    public string TenantId { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public long EventsReceived { get; set; }

    public long EventsDelivered { get; set; }

    public long EventsFailed { get; set; }

    public int MonthlyEventLimit { get; set; }

    public BillingPlan Plan { get; set; }
}
