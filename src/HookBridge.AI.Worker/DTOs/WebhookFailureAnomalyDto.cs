namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookFailureAnomalyDto
{
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double BaselineValue { get; set; }
    public double PercentageIncrease { get; set; }
    public int ScoreImpact { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
