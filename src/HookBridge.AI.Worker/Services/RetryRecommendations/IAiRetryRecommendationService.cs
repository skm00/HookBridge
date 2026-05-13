using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.RetryRecommendations;

public interface IAiRetryRecommendationService
{
    Task<WebhookFailureAnalysisResponseDto> AnalyzeAsync(
        WebhookFailureAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}
