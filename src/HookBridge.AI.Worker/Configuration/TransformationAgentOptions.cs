namespace HookBridge.AI.Worker.Configuration;

public sealed class TransformationAgentOptions
{
    public const string SectionName = "TransformationAgent";

    public bool Enabled { get; set; } = true;
    public double MinimumReadyConfidenceScore { get; set; } = 0.80;
    public double MinimumReviewConfidenceScore { get; set; } = 0.60;
    public bool RequireApprovalForGeneratedCode { get; set; } = true;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public int MaxPayloadLength { get; set; } = 4000;
    public int MaxSchemaLength { get; set; } = 4000;
}
