using System.ComponentModel.DataAnnotations;

namespace HookBridge.Api.Configuration;

/// <summary>
/// Options that control AI dashboard summary date windows and recent finding limits.
/// </summary>
public sealed class AiDashboardOptions
{
    public const string SectionName = "AiDashboard";

    [Range(1, 2160)]
    public int DefaultLookbackHours { get; set; } = 24;

    [Range(1, 365)]
    public int MaxLookbackDays { get; set; } = 90;

    [Range(1, 100)]
    public int RecentFindingsLimit { get; set; } = 20;
}
