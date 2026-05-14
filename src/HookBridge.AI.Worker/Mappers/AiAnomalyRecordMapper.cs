using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Mappers;

public static class AiAnomalyRecordMapper
{
    public static AiAnomalyRecord FromAnomalyEvent(AiAnomalyEventDto anomalyEvent, DateTime? storedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(anomalyEvent);

        return new AiAnomalyRecord
        {
            AnomalyId = anomalyEvent.AnomalyId,
            EventId = anomalyEvent.EventId,
            CorrelationId = anomalyEvent.CorrelationId,
            CustomerId = anomalyEvent.CustomerId,
            CustomerIdType = anomalyEvent.CustomerIdType,
            SubscriptionId = anomalyEvent.SubscriptionId,
            EndpointId = anomalyEvent.EndpointId,
            TargetUrl = anomalyEvent.TargetUrl,
            Environment = anomalyEvent.Environment,
            EventType = anomalyEvent.EventType,
            AnomalyType = anomalyEvent.AnomalyType.ToString(),
            RiskLevel = anomalyEvent.RiskLevel.ToString(),
            AnomalyScore = anomalyEvent.AnomalyScore,
            Summary = anomalyEvent.Summary,
            Recommendation = anomalyEvent.Recommendation,
            Source = anomalyEvent.Source,
            CreatedAtUtc = DateTime.SpecifyKind(anomalyEvent.CreatedAtUtc, DateTimeKind.Utc),
            StoredAtUtc = DateTime.SpecifyKind(storedAtUtc ?? DateTime.UtcNow, DateTimeKind.Utc)
        };
    }

    public static AiAnomalyRecord FromWebhookFailureAnomalyDetectionResponse(
        WebhookFailureAnomalyDetectionResponseDto response,
        string source = "HookBridge.AI.Worker",
        DateTime? storedAtUtc = null)
        => FromAnomalyEvent(
            AiAnomalyEventMapper.FromWebhookFailureAnomalyDetectionResponse(response, source),
            storedAtUtc);
}
