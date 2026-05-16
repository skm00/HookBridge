using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.Confidence;

public interface IAiConfidenceScoreService
{
    AiConfidenceScoreResponseDto Calculate(AiConfidenceScoreRequestDto request);
    AiConfidenceLevel MapConfidenceLevel(double confidenceScore);
    bool RequiresManualReview(double confidenceScore, AiRiskLevel riskLevel);
    bool RequiresNeedsMoreInfoOrManualReview(double confidenceScore);
}
