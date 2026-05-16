namespace HookBridge.AI.Worker.DTOs;

public sealed class HumanApprovalWorkflowApplyRequestDto
{
    public string AppliedBy { get; set; } = string.Empty;
    public string? ApplyComment { get; set; }
}
