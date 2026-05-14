using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;

namespace HookBridge.AI.Worker.Tests;

public sealed class CustomerEndpointRiskScoringServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);
    private readonly CustomerEndpointRiskScoringService _service = new();

    [Fact]
    public void CalculateRiskScore_WithSuccessfulDeliveries_ReturnsLowRisk()
    {
        var response = _service.CalculateRiskScore(Request(total: 100, success: 100), Now);
        response.RiskScore.Should().Be(0);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.HealthStatus.Should().Be(EndpointHealthStatus.Healthy);
    }

    [Fact]
    public void CalculateRiskScore_WithModerateFailures_ReturnsMediumRisk()
    {
        var response = _service.CalculateRiskScore(Request(total: 100, success: 85, failed: 15, retry: 80), Now);
        response.RiskScore.Should().BeInRange(21, 50);
        response.RiskLevel.Should().Be(AiRiskLevel.Medium);
    }

    [Fact]
    public void CalculateRiskScore_WithRepeatedFailures_ReturnsHighRisk()
    {
        var request = Request(total: 100, success: 60, failed: 40, retry: 60, timeout: 5, server: 5, dead: 2, p95: 3_000);
        var response = _service.CalculateRiskScore(request, Now);
        response.RiskScore.Should().BeInRange(51, 80);
        response.RiskLevel.Should().Be(AiRiskLevel.High);
        response.HealthStatus.Should().Be(EndpointHealthStatus.Unhealthy);
    }

    [Fact]
    public void CalculateRiskScore_WithSevereSignals_ReturnsCriticalRisk()
    {
        var request = Request(total: 100, success: 10, failed: 90, retry: 200, maxRetry: 5, timeout: 10, rateLimit: 10, client: 8, server: 10, auth: 3, sig: 2, suspicious: 2, dead: 10, avg: 2_000, p95: 6_000, lastStatus: 403, lastFailed: Now.AddMinutes(-10));
        var response = _service.CalculateRiskScore(request, Now);
        response.RiskScore.Should().Be(100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
        response.HealthStatus.Should().Be(EndpointHealthStatus.Critical);
    }

    [Fact]
    public void CalculateRiskScore_WithNoDeliveries_ReturnsUnknown()
    {
        var response = _service.CalculateRiskScore(Request(total: 0), Now);
        response.RiskLevel.Should().Be(AiRiskLevel.Unknown);
        response.HealthStatus.Should().Be(EndpointHealthStatus.Unknown);
    }

    [Fact]
    public void CalculateRiskScore_ClampsScoreAt100()
    {
        var response = _service.CalculateRiskScore(Request(total: 1, failed: 1, retry: 999, maxRetry: 1, timeout: 999, rateLimit: 999, client: 999, server: 999, auth: 999, sig: 999, suspicious: 999, dead: 999, avg: 10_000, p95: 20_000, lastFailed: Now), Now);
        response.RiskScore.Should().Be(100);
    }

    [Theory]
    [InlineData(0, AiRiskLevel.Low)]
    [InlineData(20, AiRiskLevel.Low)]
    [InlineData(21, AiRiskLevel.Medium)]
    [InlineData(50, AiRiskLevel.Medium)]
    [InlineData(51, AiRiskLevel.High)]
    [InlineData(80, AiRiskLevel.High)]
    [InlineData(81, AiRiskLevel.Critical)]
    [InlineData(100, AiRiskLevel.Critical)]
    public void MapRiskLevel_UsesConfiguredThresholds(int score, AiRiskLevel expected)
        => CustomerEndpointRiskScoringService.MapRiskLevel(score).Should().Be(expected);

    [Theory]
    [InlineData(AiRiskLevel.Low, EndpointHealthStatus.Healthy)]
    [InlineData(AiRiskLevel.Medium, EndpointHealthStatus.Degraded)]
    [InlineData(AiRiskLevel.High, EndpointHealthStatus.Unhealthy)]
    [InlineData(AiRiskLevel.Critical, EndpointHealthStatus.Critical)]
    [InlineData(AiRiskLevel.Unknown, EndpointHealthStatus.Unknown)]
    public void MapHealthStatus_UsesRiskLevel(AiRiskLevel riskLevel, EndpointHealthStatus expected)
        => CustomerEndpointRiskScoringService.MapHealthStatus(riskLevel).Should().Be(expected);

    [Theory]
    [InlineData("RateLimitFailures", 429)]
    [InlineData("ServerErrors", 500)]
    [InlineData("ClientErrors", 400)]
    [InlineData("AuthenticationFailures", 401)]
    [InlineData("AuthenticationFailures", 403)]
    public void CalculateRiskScore_GeneratesStatusCodeRiskFactors(string factorName, int statusCode)
    {
        var response = _service.CalculateRiskScore(Request(total: 10, success: 9, failed: 1, lastStatus: statusCode), Now);
        response.RiskFactors.Should().Contain(factor => factor.FactorName == factorName);
    }

    [Theory]
    [InlineData("TimeoutFailures")]
    [InlineData("SignatureValidationFailures")]
    [InlineData("SuspiciousPayloads")]
    [InlineData("DeadLetterRecords")]
    [InlineData("HighAverageLatency")]
    [InlineData("HighP95Latency")]
    public void CalculateRiskScore_GeneratesMetricRiskFactors(string factorName)
    {
        var request = factorName switch
        {
            "TimeoutFailures" => Request(total: 10, success: 9, failed: 1, timeout: 1),
            "SignatureValidationFailures" => Request(total: 10, success: 9, failed: 1, sig: 1),
            "SuspiciousPayloads" => Request(total: 10, success: 9, failed: 1, suspicious: 1),
            "DeadLetterRecords" => Request(total: 10, success: 9, failed: 1, dead: 1),
            "HighAverageLatency" => Request(total: 10, success: 10, avg: 1_200),
            _ => Request(total: 10, success: 10, p95: 2_500)
        };

        var response = _service.CalculateRiskScore(request, Now);
        response.RiskFactors.Should().Contain(factor => factor.FactorName == factorName);
    }

    [Fact]
    public void CalculateRiskScore_WithInvalidDeliveryCounts_Throws()
        => Assert.Throws<ArgumentException>(() => _service.CalculateRiskScore(Request(total: 10, success: 9, failed: 2), Now));

    [Fact]
    public void CalculateRiskScore_WithInvalidEvaluationWindow_Throws()
    {
        var request = Request(total: 10, success: 10);
        request.EvaluationWindowToUtc = request.EvaluationWindowFromUtc;
        Assert.Throws<ArgumentException>(() => _service.CalculateRiskScore(request, Now));
    }

    [Fact]
    public void CalculateRiskScore_WithInvalidTargetUrl_Throws()
    {
        var request = Request(total: 10, success: 10);
        request.TargetUrl = "not a url";
        Assert.Throws<ArgumentException>(() => _service.CalculateRiskScore(request, Now));
    }

    private static CustomerEndpointRiskScoreRequestDto Request(int total, int success = 0, int failed = 0, int retry = 0, int maxRetry = 0, int timeout = 0, int rateLimit = 0, int client = 0, int server = 0, int auth = 0, int sig = 0, int suspicious = 0, int dead = 0, double avg = 0, double p95 = 0, int? lastStatus = null, DateTime? lastFailed = null)
        => new()
        {
            CustomerId = "cust-1",
            CustomerIdType = "MDM",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            TotalDeliveries = total,
            SuccessfulDeliveries = success,
            FailedDeliveries = failed,
            RetryCount = retry,
            MaxRetryCount = maxRetry,
            TimeoutCount = timeout,
            RateLimitCount = rateLimit,
            ClientErrorCount = client,
            ServerErrorCount = server,
            AuthenticationFailureCount = auth,
            SignatureValidationFailureCount = sig,
            SuspiciousPayloadCount = suspicious,
            DeadLetterCount = dead,
            AverageLatencyMs = avg,
            P95LatencyMs = p95,
            LastStatusCode = lastStatus,
            LastFailedDeliveryAtUtc = lastFailed,
            EvaluationWindowFromUtc = Now.AddHours(-12),
            EvaluationWindowToUtc = Now
        };
}
