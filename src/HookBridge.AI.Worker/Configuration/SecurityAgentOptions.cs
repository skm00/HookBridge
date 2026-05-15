namespace HookBridge.AI.Worker.Configuration;

public sealed class SecurityAgentOptions
{
    public const string SectionName = "SecurityAgent";

    public bool Enabled { get; set; } = true;
    public long LargePayloadThresholdBytes { get; set; } = 1_048_576;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public bool RequireApprovalForReplay { get; set; } = true;
    public bool PublishAnomalyForHighRisk { get; set; } = true;
    public bool PublishAnomalyForCriticalRisk { get; set; } = true;
}
