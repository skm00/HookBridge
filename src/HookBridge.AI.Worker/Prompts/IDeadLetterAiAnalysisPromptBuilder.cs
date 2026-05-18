using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IDeadLetterAiAnalysisPromptBuilder
{
    string BuildPrompt(DeadLetterAiAnalysisRequestDto request);
    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken = default);
}
