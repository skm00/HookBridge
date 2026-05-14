using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IAiSecurityAnalysisPromptBuilder
{
    string BuildPrompt(AiSecurityAnalysisRequestDto request);
}
