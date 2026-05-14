using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IFluentValidationPromptBuilder
{
    string BuildPrompt(FluentValidationRuleGenerationRequestDto request);
}
