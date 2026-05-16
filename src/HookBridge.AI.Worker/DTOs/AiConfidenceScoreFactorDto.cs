namespace HookBridge.AI.Worker.DTOs;

public sealed class AiConfidenceScoreFactorDto
{
    public string FactorName { get; set; } = string.Empty;
    public double Impact { get; set; }
    public string Description { get; set; } = string.Empty;
}
