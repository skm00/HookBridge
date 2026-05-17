namespace HookBridge.AI.Worker.Configuration;

public sealed class AiSafeModeOptions
{
    public const string SectionName = "AiSafeMode";

    public bool Enabled { get; set; } = true;
    public string Environment { get; set; } = "production";
    public bool BlockProductionActions { get; set; } = true;
    public bool RequireApprovalForAllProductionActions { get; set; } = true;
    public bool AllowReadOnlyActions { get; set; } = true;
    public bool AllowLowRiskActionsInNonProduction { get; set; } = false;
    public bool AllowAutoApplyInDevelopment { get; set; } = false;
    public bool LogBlockedActions { get; set; } = true;
    public bool AuditBlockedActions { get; set; } = true;
}
