using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mappers;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnomalyEventMapperTests
{
    [Fact]
    public void FromWebhookFailureAnomalyDetectionResponse_MapsMetadataRiskLevelAndAnomalyType()
    {
        var response = new WebhookFailureAnomalyDetectionResponseDto
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            CustomerId = "cust-1",
            CustomerIdType = "MDM",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            EventType = "OrderCreated",
            IsAnomalyDetected = true,
            AnomalyScore = 78,
            RiskLevel = AiRiskLevel.High,
            Summary = "Rate limits spiked.",
            Recommendation = "Reduce concurrency.",
            CalculatedAtUtc = new DateTime(2026, 5, 14, 10, 16, 30, DateTimeKind.Utc),
            DetectedAnomalies =
            [
                new WebhookFailureAnomalyDto { MetricName = "RateLimitCount", ScoreImpact = 15 },
                new WebhookFailureAnomalyDto { MetricName = "RetryCount", ScoreImpact = 10 }
            ]
        };

        var dto = AiAnomalyEventMapper.FromWebhookFailureAnomalyDetectionResponse(response);

        dto.AnomalyId.Should().Be("anm_corr-1");
        dto.EventId.Should().Be("evt-1");
        dto.CorrelationId.Should().Be("corr-1");
        dto.CustomerId.Should().Be("cust-1");
        dto.RiskLevel.Should().Be(AiRiskLevel.High);
        dto.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike);
        dto.CreatedAtUtc.Should().Be(response.CalculatedAtUtc);
    }

    [Theory]
    [InlineData("FailureRate", AiAnomalyType.FailureSpike)]
    [InlineData("RetryCount", AiAnomalyType.RetrySpike)]
    [InlineData("DeadLetterCount", AiAnomalyType.DeadLetterSpike)]
    [InlineData("TimeoutCount", AiAnomalyType.TimeoutSpike)]
    [InlineData("P95LatencyMs", AiAnomalyType.LatencySpike)]
    [InlineData("NewMetric", AiAnomalyType.Unknown)]
    public void MapAnomalyType_MapsMetricNames(string metricName, AiAnomalyType expected)
        => AiAnomalyEventMapper.MapAnomalyType(metricName).Should().Be(expected);
}
