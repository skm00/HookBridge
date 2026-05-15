namespace HookBridge.AI.Worker.Configuration;

public sealed class RetryAgentOptions
{
    public const string SectionName = "RetryAgent";

    public bool Enabled { get; set; } = true;
    public int BaseDelaySeconds { get; set; } = 30;
    public int MaxDelaySeconds { get; set; } = 3600;
    public int FixedDelaySeconds { get; set; } = 60;
    public bool EnableJitter { get; set; }
    public int JitterPercentage { get; set; } = 10;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public bool AllowImmediateRetryForLowRisk { get; set; }
}
