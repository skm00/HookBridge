using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Mapping;

public static class WebhookFailureAnalysisMapper
{
    public static WebhookFailureAnalysisRequestDto ToWebhookFailureAnalysisRequest(
        AiAnalysisEventDto analysisEvent)
    {
        ArgumentNullException.ThrowIfNull(analysisEvent);

        return new WebhookFailureAnalysisRequestDto
        {
            EventId = analysisEvent.EventId,
            CorrelationId = analysisEvent.CorrelationId,
            EventType = analysisEvent.EventType,
            Source = analysisEvent.Source,
            FailureReason = analysisEvent.FailureReason,
            RequestPayload = analysisEvent.Payload,
            FailedAtUtc = analysisEvent.CreatedAtUtc.UtcDateTime
        };
    }

    public static AiAnalysisResult ToAiAnalysisResult(
        WebhookFailureAnalysisResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AiAnalysisResult
        {
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            AiSummary = response.AiSummary,
            RootCause = response.RootCause,
            AiRecommendation = response.AiRecommendation,
            RiskLevel = response.RiskLevel.ToString(),
            ConfidenceScore = response.ConfidenceScore,
            SuggestedRetryAction = response.SuggestedRetryAction.ToString(),
            IsRetryRecommended = response.IsRetryRecommended,
            Model = response.Model,
            Provider = response.Provider,
            CreatedAtUtc = EnsureUtc(response.GeneratedAtUtc)
        };
    }

    public static AiAnalysisResult ToAiAnalysisResultPlaceholder(
        WebhookFailureAnalysisRequestDto request,
        AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        return new AiAnalysisResult
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Source = request.Source ?? string.Empty,
            EventType = request.EventType,
            FailureReason = request.FailureReason,
            AiSummary = string.IsNullOrWhiteSpace(request.FailureReason)
                ? "AI analysis placeholder created for webhook failure analysis."
                : $"AI analysis placeholder created for failure: {request.FailureReason}",
            RootCause = string.Empty,
            AiRecommendation = "Review the webhook payload, delivery history, and target endpoint health before retrying.",
            RiskLevel = AiRiskLevel.Unknown.ToString(),
            ConfidenceScore = 0,
            SuggestedRetryAction = SuggestedRetryAction.RequireManualReview.ToString(),
            IsRetryRecommended = false,
            Model = options.Model,
            Provider = options.Provider,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
