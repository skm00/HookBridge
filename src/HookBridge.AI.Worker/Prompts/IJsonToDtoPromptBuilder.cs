using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IJsonToDtoPromptBuilder
{
    string BuildPrompt(JsonToDtoSuggestionRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(JsonToDtoSuggestionRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.JsonToDtoSuggestion,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
