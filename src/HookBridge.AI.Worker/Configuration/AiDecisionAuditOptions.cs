namespace HookBridge.AI.Worker.Configuration;

public sealed class AiDecisionAuditOptions
{
    public const string SectionName = "AiDecisionAudit";

    public bool Enabled { get; set; } = true;
    public bool AuditFallbackDecisions { get; set; } = true;
    public bool AuditSafeModeEvaluations { get; set; } = true;
    public bool AuditHumanApprovals { get; set; } = true;
    public bool AuditNaturalLanguageQueries { get; set; } = true;
    public int MaxMetadataLength { get; set; } = 4000;
    public bool IncludePromptMetadata { get; set; } = true;
    public bool IncludeModelMetadata { get; set; } = true;
}
