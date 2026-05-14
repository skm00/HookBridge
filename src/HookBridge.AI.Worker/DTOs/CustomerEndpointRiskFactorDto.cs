namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Explains a deterministic contributor to a customer endpoint risk score.
/// </summary>
public sealed class CustomerEndpointRiskFactorDto
{
    public string FactorName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public int ScoreImpact { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;
}
