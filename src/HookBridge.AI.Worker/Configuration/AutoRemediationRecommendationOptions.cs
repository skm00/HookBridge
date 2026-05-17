namespace HookBridge.AI.Worker.Configuration;

public sealed class AutoRemediationRecommendationOptions
{
    public const string SectionName = "AutoRemediationRecommendation";
    public bool Enabled { get; set; } = true;
    public bool AllowAutoApplyLowRisk { get; set; } = false;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public bool RequireApprovalForSecurityActions { get; set; } = true;
    public bool RequireApprovalForEndpointPause { get; set; } = true;
    public bool RequireApprovalForDeadLetterActions { get; set; } = true;
    public double LowConfidenceThreshold { get; set; } = 0.60;
    public long KafkaLagThreshold { get; set; } = 1000;
    public long MongoLatencyThresholdMs { get; set; } = 1000;
}
