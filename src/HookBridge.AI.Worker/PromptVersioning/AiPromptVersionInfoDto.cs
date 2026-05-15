namespace HookBridge.AI.Worker.PromptVersioning;

public sealed class AiPromptVersionInfoDto
{
    public string PromptName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string? Content { get; set; }
}
