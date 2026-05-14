using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IJsonToDtoPromptBuilder
{
    string BuildPrompt(JsonToDtoSuggestionRequestDto request);
}
