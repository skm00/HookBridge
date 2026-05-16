namespace HookBridge.AI.Worker.Configuration;

public sealed class AiConfidenceScoreOptions
{
    public const string SectionName = "AiConfidenceScore";

    public bool Enabled { get; set; } = true;
    public double BaseScore { get; set; } = 0.75;
    public double FallbackPenalty { get; set; } = 0.15;
    public double MissingDataPenalty { get; set; } = 0.05;
    public double ValidationIssuePenalty { get; set; } = 0.05;
    public double FailedAgentPenalty { get; set; } = 0.10;
    public double LowConfidenceReviewThreshold { get; set; } = 0.60;
    public double VeryLowConfidenceReviewThreshold { get; set; } = 0.40;
}
