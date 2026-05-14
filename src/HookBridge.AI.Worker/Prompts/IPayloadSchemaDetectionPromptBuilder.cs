using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IPayloadSchemaDetectionPromptBuilder
{
    string BuildPrompt(PayloadSchemaDetectionRequestDto request);
}
