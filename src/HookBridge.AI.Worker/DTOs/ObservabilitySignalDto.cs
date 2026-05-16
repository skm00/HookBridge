namespace HookBridge.AI.Worker.DTOs;

public sealed class ObservabilitySignalDto
{
    public string SignalName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
