using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.SecurityAnalysis;

public interface IAiSecurityAnalysisAgent
{
    Task<AiSecurityAnalysisResponseDto> AnalyzeAsync(AiSecurityAnalysisRequestDto request, CancellationToken cancellationToken = default);
}
