using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.JsonToDtoSuggestion;

public interface IJsonToDtoSuggestionAgent
{
    Task<JsonToDtoSuggestionResponseDto> SuggestAsync(
        JsonToDtoSuggestionRequestDto request,
        CancellationToken cancellationToken = default);
}
