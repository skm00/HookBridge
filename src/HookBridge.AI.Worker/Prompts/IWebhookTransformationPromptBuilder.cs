using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IWebhookTransformationPromptBuilder
{
    string BuildPrompt(WebhookTransformationRecommendationRequestDto request);
}
