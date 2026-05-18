using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;

public interface IDeadLetterAiAnalysisService
{
    Task<DeadLetterAiAnalysisResponseDto> AnalyzeAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken = default);
}
