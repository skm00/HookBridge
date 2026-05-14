using HookBridge.Application.DTOs.AiNaturalLanguageQuery;

namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public interface IAiNaturalLanguageQueryPromptBuilder
{
    string BuildPrompt(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results);
}
