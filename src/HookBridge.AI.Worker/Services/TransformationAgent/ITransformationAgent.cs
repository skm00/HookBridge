using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.TransformationAgent;

public interface ITransformationAgent
{
    Task<TransformationAgentResponseDto> AnalyzeAsync(TransformationAgentRequestDto request, CancellationToken cancellationToken = default);
}
