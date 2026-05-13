using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.AI.Worker.Tests;

public sealed class EndpointHealthScoringServiceTests
{
    private static readonly DateTime CalculatedAtUtc = new(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateHealthScore_WithHealthySignals_ReturnsHealthyScore()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(
            totalDeliveries: 1_000,
            successfulDeliveries: 995,
            failedDeliveries: 5,
            averageLatencyMs: 150,
            p95LatencyMs: 300), CalculatedAtUtc);

        result.HealthScore.Should().Be(100);
        result.HealthStatus.Should().Be(EndpointHealthStatus.Healthy);
        result.RiskLevel.Should().Be(AiRiskLevel.Low);
    }

    [Fact]
    public void CalculateHealthScore_WithModerateFailures_ReturnsDegradedScore()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(
            totalDeliveries: 100,
            successfulDeliveries: 85,
            failedDeliveries: 15,
            serverErrorCount: 2,
            retryCount: 2,
            averageLatencyMs: 1_200,
            p95LatencyMs: 2_300), CalculatedAtUtc);

        result.HealthScore.Should().BeInRange(70, 89);
        result.HealthStatus.Should().Be(EndpointHealthStatus.Degraded);
        result.RiskLevel.Should().Be(AiRiskLevel.Medium);
    }

    [Fact]
    public void CalculateHealthScore_WithSignificantFailures_ReturnsUnhealthyScore()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(
            totalDeliveries: 100,
            successfulDeliveries: 60,
            failedDeliveries: 40,
            timeoutCount: 3,
            serverErrorCount: 4,
            retryCount: 5,
            averageLatencyMs: 1_500,
            p95LatencyMs: 2_600), CalculatedAtUtc);

        result.HealthScore.Should().BeInRange(40, 69);
        result.HealthStatus.Should().Be(EndpointHealthStatus.Unhealthy);
        result.RiskLevel.Should().Be(AiRiskLevel.High);
    }

    [Fact]
    public void CalculateHealthScore_WithSevereFailures_ReturnsCriticalScore()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(
            totalDeliveries: 100,
            successfulDeliveries: 20,
            failedDeliveries: 80,
            timeoutCount: 10,
            rateLimitCount: 4,
            serverErrorCount: 10,
            retryCount: 10,
            deadLetterCount: 3,
            averageLatencyMs: 4_000,
            p95LatencyMs: 7_000,
            lastFailedDeliveryAtUtc: CalculatedAtUtc.AddMinutes(-10)), CalculatedAtUtc);

        result.HealthScore.Should().BeInRange(0, 39);
        result.HealthStatus.Should().Be(EndpointHealthStatus.Critical);
        result.RiskLevel.Should().Be(AiRiskLevel.Critical);
    }

    [Fact]
    public void CalculateHealthScore_WithNoDeliveries_ReturnsUnknown()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(totalDeliveries: 0), CalculatedAtUtc);

        result.HealthScore.Should().Be(0);
        result.HealthStatus.Should().Be(EndpointHealthStatus.Unknown);
        result.RiskLevel.Should().Be(AiRiskLevel.Unknown);
        result.Recommendation.Should().Contain("Collect delivery data");
    }

    [Fact]
    public void CalculateHealthScore_WithExtremePenalties_ClampsScoreToZero()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(
            totalDeliveries: 10,
            successfulDeliveries: 0,
            failedDeliveries: 10,
            timeoutCount: 100,
            rateLimitCount: 100,
            clientErrorCount: 100,
            serverErrorCount: 100,
            retryCount: 100,
            deadLetterCount: 100,
            averageLatencyMs: 100_000,
            p95LatencyMs: 200_000,
            lastFailedDeliveryAtUtc: CalculatedAtUtc.AddMinutes(-1)), CalculatedAtUtc);

        result.HealthScore.Should().Be(0);
    }

    [Fact]
    public void CalculateHealthScore_WithRateLimitFailures_RecommendsBackoffAndReducedConcurrency()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(rateLimitCount: 1, lastFailureStatusCode: 429), CalculatedAtUtc);

        result.Recommendation.Should().Contain("exponential backoff").And.Contain("reduce delivery concurrency");
    }

    [Fact]
    public void CalculateHealthScore_WithTimeoutFailures_RecommendsTimeoutOrAvailabilityReview()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(timeoutCount: 1), CalculatedAtUtc);

        result.Recommendation.Should().Contain("Increase timeout").And.Contain("receiver availability");
    }

    [Fact]
    public void CalculateHealthScore_WithServerErrors_RecommendsRetryWithBackoffAndMonitoring()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(serverErrorCount: 1, lastFailureStatusCode: 503), CalculatedAtUtc);

        result.Recommendation.Should().Contain("Retry with backoff").And.Contain("monitor receiver health");
    }

    [Fact]
    public void CalculateHealthScore_WithClientErrors_RecommendsManualReview()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(clientErrorCount: 1, lastFailureStatusCode: 401), CalculatedAtUtc);

        result.Recommendation.Should().Contain("Manually review endpoint configuration").And.Contain("authentication").And.Contain("payload");
    }

    [Fact]
    public void CalculateHealthScore_WithDeadLetters_RecommendsManualReviewBeforeReplay()
    {
        var result = CreateService().CalculateHealthScore(CreateRequest(deadLetterCount: 1), CalculatedAtUtc);

        result.Recommendation.Should().Contain("Manually review dead-letter records before replaying deliveries");
    }

    [Fact]
    public void CalculateHealthScore_WithInvalidDeliveryCounts_ThrowsValidationException()
    {
        var request = CreateRequest(totalDeliveries: 10, successfulDeliveries: 8, failedDeliveries: 5);

        var act = () => CreateService().CalculateHealthScore(request, CalculatedAtUtc);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*SuccessfulDeliveries plus FailedDeliveries must not exceed TotalDeliveries*");
    }

    [Fact]
    public void CalculateHealthScore_WithInvalidEvaluationWindow_ThrowsValidationException()
    {
        var request = CreateRequest();
        request.EvaluationWindowToUtc = request.EvaluationWindowFromUtc;

        var act = () => CreateService().CalculateHealthScore(request, CalculatedAtUtc);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*EvaluationWindowToUtc must be greater than EvaluationWindowFromUtc*");
    }

    [Fact]
    public void CalculateHealthScore_WithNonUtcDate_ThrowsValidationException()
    {
        var request = CreateRequest();
        request.LastSuccessfulDeliveryAtUtc = DateTime.SpecifyKind(CalculatedAtUtc, DateTimeKind.Local);

        var act = () => CreateService().CalculateHealthScore(request, CalculatedAtUtc);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*LastSuccessfulDeliveryAtUtc must be a UTC DateTime*");
    }

    [Theory]
    [InlineData(95, EndpointHealthStatus.Healthy, AiRiskLevel.Low)]
    [InlineData(80, EndpointHealthStatus.Degraded, AiRiskLevel.Medium)]
    [InlineData(55, EndpointHealthStatus.Unhealthy, AiRiskLevel.High)]
    [InlineData(20, EndpointHealthStatus.Critical, AiRiskLevel.Critical)]
    public void CalculateHealthScore_MapsHealthStatusAndRiskLevel(int expectedScore, EndpointHealthStatus expectedStatus, AiRiskLevel expectedRisk)
    {
        var request = expectedScore switch
        {
            95 => CreateRequest(totalDeliveries: 100, successfulDeliveries: 90, failedDeliveries: 10),
            80 => CreateRequest(totalDeliveries: 100, successfulDeliveries: 60, failedDeliveries: 40),
            55 => CreateRequest(totalDeliveries: 100, successfulDeliveries: 50, failedDeliveries: 50, retryCount: 10, averageLatencyMs: 3_000),
            _ => CreateRequest(totalDeliveries: 100, successfulDeliveries: 20, failedDeliveries: 80, serverErrorCount: 10, deadLetterCount: 2)
        };

        var result = CreateService().CalculateHealthScore(request, CalculatedAtUtc);

        result.HealthScore.Should().Be(expectedScore);
        result.HealthStatus.Should().Be(expectedStatus);
        result.RiskLevel.Should().Be(expectedRisk);
    }

    [Fact]
    public void AddEndpointHealthScoringServices_RegistersHealthScoringService()
    {
        var services = new ServiceCollection();

        services.AddEndpointHealthScoringServices();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEndpointHealthScoringService>()
            .Should().BeOfType<EndpointHealthScoringService>();
    }

    private static EndpointHealthScoringService CreateService() => new();

    private static EndpointHealthScoreRequestDto CreateRequest(
        int totalDeliveries = 100,
        int? successfulDeliveries = null,
        int? failedDeliveries = null,
        int timeoutCount = 0,
        int rateLimitCount = 0,
        int clientErrorCount = 0,
        int serverErrorCount = 0,
        int retryCount = 0,
        int deadLetterCount = 0,
        double averageLatencyMs = 100,
        double p95LatencyMs = 200,
        int? lastFailureStatusCode = null,
        DateTime? lastFailedDeliveryAtUtc = null)
    {
        var successes = successfulDeliveries ?? totalDeliveries;
        var failures = failedDeliveries ?? 0;

        return new EndpointHealthScoreRequestDto
        {
            EndpointId = "endpoint_123",
            SubscriptionId = "sub_456",
            CustomerId = "cust_789",
            CustomerIdType = "internal",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            TotalDeliveries = totalDeliveries,
            SuccessfulDeliveries = successes,
            FailedDeliveries = failures,
            TimeoutCount = timeoutCount,
            RateLimitCount = rateLimitCount,
            ClientErrorCount = clientErrorCount,
            ServerErrorCount = serverErrorCount,
            RetryCount = retryCount,
            DeadLetterCount = deadLetterCount,
            AverageLatencyMs = averageLatencyMs,
            P95LatencyMs = p95LatencyMs,
            LastFailureStatusCode = lastFailureStatusCode,
            LastFailureReason = failures > 0 ? "delivery failed" : null,
            LastSuccessfulDeliveryAtUtc = successes > 0 ? CalculatedAtUtc.AddMinutes(-20) : null,
            LastFailedDeliveryAtUtc = lastFailedDeliveryAtUtc,
            EvaluationWindowFromUtc = CalculatedAtUtc.AddHours(-1),
            EvaluationWindowToUtc = CalculatedAtUtc
        };
    }
}
