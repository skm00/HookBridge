using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.SecurityAgent;

public interface ISecurityAgent
{
    Task<SecurityAgentResponseDto> AnalyzeAsync(SecurityAgentRequestDto request, CancellationToken cancellationToken = default);
}
