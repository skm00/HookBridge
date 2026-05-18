namespace HookBridge.AI.Worker.Configuration;

public sealed class DeadLetterAiAnalysisOptions
{
    public const string SectionName = "DeadLetterAiAnalysis";

    public bool Enabled { get; set; } = true;
    public bool EnableAiAnalysis { get; set; } = true;
    public int MaxPayloadLength { get; set; } = 4000;
    public int MaxResponseBodyLength { get; set; } = 2000;
    public bool RequireApprovalForReplay { get; set; } = true;
    public bool RequireApprovalForHighRisk { get; set; } = true;
    public bool RequireApprovalForCriticalRisk { get; set; } = true;
    public bool RequireApprovalForSuspiciousEvents { get; set; } = true;
}
