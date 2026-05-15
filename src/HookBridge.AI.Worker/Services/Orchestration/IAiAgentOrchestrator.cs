using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.Orchestration;

public interface IAiAgentOrchestrator
{
    Task<AiAgentOrchestrationResponseDto> OrchestrateAsync(
        AiAgentOrchestrationRequestDto request,
        CancellationToken cancellationToken = default);
}
