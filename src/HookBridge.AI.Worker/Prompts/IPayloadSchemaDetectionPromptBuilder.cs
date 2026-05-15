using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public interface IPayloadSchemaDetectionPromptBuilder
{
    string BuildPrompt(PayloadSchemaDetectionRequestDto request);

    Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(PayloadSchemaDetectionRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new AiPromptBuildResult
        {
            Content = BuildPrompt(request),
            Metadata = new()
            {
                PromptName = HookBridge.AI.Worker.PromptVersioning.AiPromptNames.PayloadSchemaDetection,
                Version = HookBridge.AI.Worker.PromptVersioning.AiPromptOptions.DefaultPromptVersion
            }
        });
}
