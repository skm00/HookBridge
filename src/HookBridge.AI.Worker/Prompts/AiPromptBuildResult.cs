using HookBridge.AI.Worker.PromptVersioning;

namespace HookBridge.AI.Worker.Prompts;

public sealed class AiPromptBuildResult
{
    public string Content { get; set; } = string.Empty;
    public AiPromptVersionInfoDto Metadata { get; set; } = new();
}
