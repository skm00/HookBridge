using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.EndpointHealthScoring;

public interface IEndpointHealthScoringService
{
    EndpointHealthScoreResponseDto CalculateHealthScore(
        EndpointHealthScoreRequestDto request,
        DateTime calculatedAtUtc);
}
