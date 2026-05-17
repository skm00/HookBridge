using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.AutoRemediationRecommendation;

public interface IAutoRemediationRecommendationService
{
    Task<AutoRemediationRecommendationResponseDto> RecommendAsync(AutoRemediationRecommendationRequestDto request, CancellationToken cancellationToken = default);
}
