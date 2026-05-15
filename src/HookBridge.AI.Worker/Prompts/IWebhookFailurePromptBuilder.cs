using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IWebhookFailurePromptBuilder
{
    string BuildPrompt(WebhookFailureAnalysisRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(WebhookFailureAnalysisRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.WebhookFailureAnalysis,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
