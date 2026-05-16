namespace HookBridge.AI.Worker.Configuration;

public sealed class HumanApprovalWorkflowOptions
{
    public const string SectionName = "HumanApprovalWorkflow";

    public bool Enabled { get; set; } = true;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public bool RequireApprovalForSecurityActions { get; set; } = true;
    public bool RequireApprovalForTransformations { get; set; } = true;
    public bool AllowLowRiskAutoApproval { get; set; }
    public int ApprovalExpiryHours { get; set; } = 72;
    public bool AllowApplyOnlyAfterApproval { get; set; } = true;
}
