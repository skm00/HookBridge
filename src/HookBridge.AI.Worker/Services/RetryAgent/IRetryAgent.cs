using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.RetryAgent;

public interface IRetryAgent
{
    Task<RetryAgentResponseDto> AnalyzeAsync(RetryAgentRequestDto request, CancellationToken cancellationToken = default);
}
