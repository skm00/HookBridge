using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.LogSummaries;

public interface IAiLogSummarizationService
{
    Task<AiLogSummaryResponseDto> SummarizeAsync(
        AiLogSummaryRequestDto request,
        CancellationToken cancellationToken = default);
}
