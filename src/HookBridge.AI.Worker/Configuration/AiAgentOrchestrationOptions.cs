using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Configuration;

public sealed class AiAgentOrchestrationOptions
{
    public const string SectionName = "AiAgentOrchestration";

    public bool Enabled { get; set; } = true;
    public AiOrchestrationMode Mode { get; set; } = AiOrchestrationMode.Sequential;
    public bool EnableRetryAgent { get; set; } = true;
    public bool EnableSecurityAgent { get; set; } = true;
    public bool EnableDuplicateReplayAgent { get; set; } = true;
    public bool EnablePayloadSchemaAgent { get; set; } = true;
    public bool EnableEndpointRiskAgent { get; set; } = true;
    public bool EnableAnomalyAgent { get; set; } = true;
    public bool EnableLogSummaryAgent { get; set; } = false;
    public bool EnableTransformationAgent { get; set; } = false;
    public int AgentTimeoutSeconds { get; set; } = 30;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
}
