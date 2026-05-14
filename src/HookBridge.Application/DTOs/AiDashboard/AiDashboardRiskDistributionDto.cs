namespace HookBridge.Application.DTOs.AiDashboard;

/// <summary>
/// Counts AI findings by normalized risk level.
/// </summary>
public sealed class AiDashboardRiskDistributionDto
{
    public long Unknown { get; set; }
    public long Low { get; set; }
    public long Medium { get; set; }
    public long High { get; set; }
    public long Critical { get; set; }
}
