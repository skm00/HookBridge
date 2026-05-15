using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IAiSecurityAnalysisPromptBuilder
{
    string BuildPrompt(AiSecurityAnalysisRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(AiSecurityAnalysisRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.AiSecurityAnalysis,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
