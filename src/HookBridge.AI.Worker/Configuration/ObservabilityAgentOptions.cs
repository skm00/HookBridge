namespace HookBridge.AI.Worker.Configuration;

public sealed class ObservabilityAgentOptions
{
    public const string SectionName = "ObservabilityAgent";

    public bool Enabled { get; set; } = true;
    public long KafkaLagDegradedThreshold { get; set; } = 1000;
    public long KafkaLagCriticalThreshold { get; set; } = 10000;
    public long MongoLatencyDegradedThresholdMs { get; set; } = 1000;
    public long MongoLatencyUnhealthyThresholdMs { get; set; } = 5000;
    public double FailureRateDegradedThresholdPercent { get; set; } = 10;
    public double FailureRateUnhealthyThresholdPercent { get; set; } = 30;
    public int ErrorLogCriticalThreshold { get; set; } = 50;
    public int SecurityFindingUnhealthyThreshold { get; set; } = 10;
    public bool RequireApprovalForCriticalStatus { get; set; } = true;
    public bool EnableAiLogSummary { get; set; } = true;
}
