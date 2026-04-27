namespace HookBridge.Application.DTOs.Dashboard;

public sealed class DashboardOverviewResponseDto
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public int MonthlyEventLimit { get; set; }
    public long EventsReceivedThisMonth { get; set; }
    public long EventsDeliveredThisMonth { get; set; }
    public long EventsFailedThisMonth { get; set; }
    public long TotalDeliveryAttemptsThisMonth { get; set; }
    public long SuccessfulDeliveryAttemptsThisMonth { get; set; }
    public long FailedDeliveryAttemptsThisMonth { get; set; }
    public long FailedEventsInDlq { get; set; }
    public double SuccessRate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}
