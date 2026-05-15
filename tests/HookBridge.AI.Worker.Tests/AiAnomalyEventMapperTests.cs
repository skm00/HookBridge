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
        dto.CustomerIdType.Should().Be("MDM");
        dto.SubscriptionId.Should().Be("sub-1");
        dto.EndpointId.Should().Be("endpoint-1");
        dto.TargetUrl.Should().Be("https://customer.example.com/webhook");
        dto.Environment.Should().Be("qa");
        dto.EventType.Should().Be("OrderCreated");
        dto.RiskLevel.Should().Be(AiRiskLevel.High);
        dto.AnomalyScore.Should().Be(78);
        dto.Summary.Should().Be("Rate limits spiked.");
        dto.Recommendation.Should().Be("Reduce concurrency.");
        dto.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike);
        dto.Source.Should().Be("HookBridge.AI.Worker");
        dto.CreatedAtUtc.Should().Be(response.CalculatedAtUtc);
    }

    [Theory]
    [InlineData("FailureRate", AiAnomalyType.FailureSpike)]
    [InlineData("RetryCount", AiAnomalyType.RetrySpike)]
    [InlineData("DeadLetterCount", AiAnomalyType.DeadLetterSpike)]
    [InlineData("TimeoutCount", AiAnomalyType.TimeoutSpike)]
    [InlineData("RateLimitCount", AiAnomalyType.RateLimitSpike)]
    [InlineData("ServerErrorCount", AiAnomalyType.ServerErrorSpike)]
    [InlineData("ClientErrorCount", AiAnomalyType.ClientErrorSpike)]
    [InlineData("AuthenticationFailureCount", AiAnomalyType.AuthenticationFailureSpike)]
    [InlineData("SignatureValidationFailureCount", AiAnomalyType.SignatureValidationSpike)]
    [InlineData("SuspiciousPayloadCount", AiAnomalyType.SuspiciousPayloadSpike)]
    [InlineData("AverageLatencyMs", AiAnomalyType.LatencySpike)]
    [InlineData("P95LatencyMs", AiAnomalyType.LatencySpike)]
    [InlineData("NewMetric", AiAnomalyType.Unknown)]
    public void MapAnomalyType_MapsMetricNames(string metricName, AiAnomalyType expected)
        => AiAnomalyEventMapper.MapAnomalyType(metricName).Should().Be(expected);
}
