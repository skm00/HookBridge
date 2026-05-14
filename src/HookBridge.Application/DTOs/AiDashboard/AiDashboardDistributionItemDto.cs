namespace HookBridge.Application.DTOs.AiDashboard;

/// <summary>
/// A named dashboard distribution bucket.
/// </summary>
public sealed class AiDashboardDistributionItemDto
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
}
