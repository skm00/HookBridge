using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.Fallback;

public interface IAiFallbackService
{
    Task<WebhookFailureAnalysisResponseDto> CreateWebhookFailureAnalysisAsync(
        WebhookFailureAnalysisRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default);

    Task<WebhookFailureAnalysisResponseDto> CreateRetryRecommendationAsync(
        WebhookFailureAnalysisRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default);

    Task<AiLogSummaryResponseDto> CreateLogSummaryAsync(
        AiLogSummaryRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default);

    Task<EndpointHealthScoreResponseDto> CreateEndpointHealthSummaryAsync(
        EndpointHealthScoreRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        CancellationToken cancellationToken = default);
}
