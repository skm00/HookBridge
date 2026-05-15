using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.PromptVersioning;

namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public interface IAiNaturalLanguageQueryPromptBuilder
{
    string BuildPrompt(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request, intent, results),
            Metadata = new AiPromptVersionInfoDto
            {
                PromptName = AiPromptNames.NaturalLanguageQuery,
                Version = AiPromptOptions.DefaultPromptVersion
            }
        });
}
