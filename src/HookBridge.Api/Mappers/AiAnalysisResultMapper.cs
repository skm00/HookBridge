using HookBridge.AI.Worker.Mongo;
using HookBridge.Application.DTOs.AiAnalysis;

namespace HookBridge.Api.Mappers;

/// <summary>
/// Maps stored AI analysis documents to API response DTOs.
/// </summary>
public static class AiAnalysisResultMapper
{
    public static AiAnalysisResultResponseDto ToResponseDto(AiAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new AiAnalysisResultResponseDto
        {
            Id = result.Id,
            EventId = result.EventId,
            CorrelationId = result.CorrelationId,
            Source = result.Source,
            EventType = result.EventType,
            FailureReason = result.FailureReason,
            AiSummary = result.AiSummary,
            RootCause = result.RootCause,
            AiRecommendation = result.AiRecommendation,
            RiskLevel = result.RiskLevel,
            ConfidenceScore = result.ConfidenceScore,
            SuggestedRetryAction = result.SuggestedRetryAction,
            IsRetryRecommended = result.IsRetryRecommended,
            Model = result.Model,
            Provider = result.Provider,
            PromptName = result.PromptName,
            PromptVersion = result.PromptVersion,
            PromptHash = result.PromptHash,
            CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc),
        };
    }
}
