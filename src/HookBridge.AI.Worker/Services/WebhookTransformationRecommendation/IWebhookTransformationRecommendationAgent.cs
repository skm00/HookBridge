using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;

public interface IWebhookTransformationRecommendationAgent
{
    Task<WebhookTransformationRecommendationResponseDto> RecommendAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default);
}
