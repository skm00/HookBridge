using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IFluentValidationPromptBuilder
{
    string BuildPrompt(FluentValidationRuleGenerationRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(FluentValidationRuleGenerationRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.FluentValidationRuleGeneration,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
