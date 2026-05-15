namespace HookBridge.AI.Worker.PromptVersioning;

public sealed class AiPromptTemplateDto
{
    public string Content { get; set; } = string.Empty;
    public AiPromptVersionInfoDto Metadata { get; set; } = new();
}
