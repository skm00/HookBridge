using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IAiLogSummaryPromptBuilder
{
    string BuildPrompt(AiLogSummaryRequestDto request);
}
