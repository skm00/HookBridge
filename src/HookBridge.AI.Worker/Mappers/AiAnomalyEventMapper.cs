using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mappers;

public static class AiAnomalyEventMapper
{
    public static AiAnomalyEventDto FromWebhookFailureAnomalyDetectionResponse(
        WebhookFailureAnomalyDetectionResponseDto response,
        string source = "HookBridge.AI.Worker")
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AiAnomalyEventDto
        {
            AnomalyId = CreateAnomalyId(response),
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            CustomerId = response.CustomerId,
            CustomerIdType = response.CustomerIdType,
            SubscriptionId = response.SubscriptionId,
            EndpointId = response.EndpointId,
            TargetUrl = response.TargetUrl,
            Environment = response.Environment,
            EventType = response.EventType,
            AnomalyType = MapAnomalyType(response.DetectedAnomalies),
            RiskLevel = response.RiskLevel,
            AnomalyScore = response.AnomalyScore,
            Summary = response.Summary,
            Recommendation = response.Recommendation,
            Source = source,
            CreatedAtUtc = response.CalculatedAtUtc
        };
    }

    public static AiAnomalyType MapAnomalyType(IEnumerable<WebhookFailureAnomalyDto>? anomalies)
    {
        var primaryMetricName = anomalies?
            .OrderByDescending(anomaly => anomaly.ScoreImpact)
            .ThenBy(anomaly => anomaly.MetricName, StringComparer.OrdinalIgnoreCase)
            .Select(anomaly => anomaly.MetricName)
            .FirstOrDefault();

        return MapAnomalyType(primaryMetricName);
    }

    public static AiAnomalyType MapAnomalyType(string? metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return AiAnomalyType.Unknown;
        }

        return metricName.Trim() switch
        {
            "FailureRate" => AiAnomalyType.FailureSpike,
            "RetryCount" => AiAnomalyType.RetrySpike,
            "DeadLetterCount" => AiAnomalyType.DeadLetterSpike,
            "RateLimitCount" => AiAnomalyType.RateLimitSpike,
            "TimeoutCount" => AiAnomalyType.TimeoutSpike,
            "ServerErrorCount" => AiAnomalyType.ServerErrorSpike,
            "ClientErrorCount" => AiAnomalyType.ClientErrorSpike,
            "AuthenticationFailureCount" => AiAnomalyType.AuthenticationFailureSpike,
            "SignatureValidationFailureCount" => AiAnomalyType.SignatureValidationSpike,
            "SuspiciousPayloadCount" => AiAnomalyType.SuspiciousPayloadSpike,
            "AverageLatencyMs" or "P95LatencyMs" => AiAnomalyType.LatencySpike,
            _ => AiAnomalyType.Unknown
        };
    }

    private static string CreateAnomalyId(WebhookFailureAnomalyDetectionResponseDto response)
    {
        var seed = !string.IsNullOrWhiteSpace(response.CorrelationId)
            ? response.CorrelationId
            : !string.IsNullOrWhiteSpace(response.EventId)
                ? response.EventId
                : Guid.NewGuid().ToString("N");

        return $"anm_{seed}";
    }
}
