using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IWebhookFailurePromptBuilder
{
    string BuildPrompt(WebhookFailureAnalysisRequestDto request);
}
