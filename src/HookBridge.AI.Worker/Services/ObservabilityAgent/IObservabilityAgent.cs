using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.ObservabilityAgent;

public interface IObservabilityAgent
{
    Task<ObservabilityAgentResponseDto> AnalyzeAsync(ObservabilityAgentRequestDto request, CancellationToken cancellationToken = default);
}
