using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IWebhookTransformationPromptBuilder
{
    string BuildPrompt(WebhookTransformationRecommendationRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.WebhookTransformationRecommendation,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
