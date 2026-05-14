using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;

public interface ICustomerEndpointRiskScoringService
{
    CustomerEndpointRiskScoreResponseDto CalculateRiskScore(CustomerEndpointRiskScoreRequestDto request, DateTime calculatedAtUtc);
}
