using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailureAnomalyDetectionServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);
    private readonly WebhookFailureAnomalyDetectionService _service = new();

    [Fact]
    public void DetectAnomalies_WhenCurrentMetricsMatchBaseline_ReturnsNoAnomaly()
    {
        var response = _service.DetectAnomalies(Request(), Now);
        response.IsAnomalyDetected.Should().BeFalse();
        response.AnomalyScore.Should().Be(0);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.DetectedAnomalies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("FailureRate")]
    [InlineData("RetryCount")]
    [InlineData("DeadLetterCount")]
    [InlineData("TimeoutCount")]
    [InlineData("RateLimitCount")]
    [InlineData("ServerErrorCount")]
    [InlineData("ClientErrorCount")]
    [InlineData("AuthenticationFailureCount")]
    [InlineData("SignatureValidationFailureCount")]
    [InlineData("SuspiciousPayloadCount")]
    [InlineData("AverageLatencyMs")]
    [InlineData("P95LatencyMs")]
    public void DetectAnomalies_DetectsConfiguredMetricSpike(string metricName)
    {
        var request = Request();
        ApplySpike(request.CurrentWindow!, metricName);

        var response = _service.DetectAnomalies(request, Now);

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == metricName);
    }

    [Fact]
    public void DetectAnomalies_ClampsScoreAt100()
    {
        var request = Request();
        foreach (var metricName in new[] { "FailureRate", "RetryCount", "DeadLetterCount", "TimeoutCount", "RateLimitCount", "ServerErrorCount", "ClientErrorCount", "AuthenticationFailureCount", "SignatureValidationFailureCount", "SuspiciousPayloadCount", "AverageLatencyMs", "P95LatencyMs" })
        {
            ApplySpike(request.CurrentWindow!, metricName);
        }

        var response = _service.DetectAnomalies(request, Now);
        response.AnomalyScore.Should().Be(100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
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
        => WebhookFailureAnomalyDetectionService.MapRiskLevel(score).Should().Be(expected);

    [Fact]
    public void DetectAnomalies_SetsDetectionFlagAtThreshold()
    {
        var below = Request();
        ApplySpike(below.CurrentWindow!, "RetryCount");
        _service.DetectAnomalies(below, Now).IsAnomalyDetected.Should().BeFalse();

        var above = Request();
        ApplySpike(above.CurrentWindow!, "FailureRate");
        ApplySpike(above.CurrentWindow!, "RateLimitCount");
        _service.DetectAnomalies(above, Now).IsAnomalyDetected.Should().BeTrue();
    }

    [Fact]
    public void DetectAnomalies_WithNoDeliveries_ReturnsUnknownRisk()
    {
        var request = Request();
        request.CurrentWindow!.TotalDeliveries = 0;
        request.CurrentWindow.FailedDeliveries = 0;
        var response = _service.DetectAnomalies(request, Now);
        response.RiskLevel.Should().Be(AiRiskLevel.Unknown);
        response.IsAnomalyDetected.Should().BeFalse();
    }

    [Fact]
    public void DetectAnomalies_WithInvalidCurrentWindow_Throws()
    {
        var request = Request();
        request.CurrentWindow!.WindowEndUtc = request.CurrentWindow.WindowStartUtc;
        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithInvalidBaselineWindow_Throws()
    {
        var request = Request();
        request.BaselineWindow!.WindowEndUtc = request.BaselineWindow.WindowStartUtc;
        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithNegativeMetric_Throws()
    {
        var request = Request();
        request.CurrentWindow!.RetryCount = -1;
        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithInvalidTargetUrl_Throws()
    {
        var request = Request();
        request.TargetUrl = "not-a-url";
        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithNonUtcDates_Throws()
    {
        var request = Request();
        request.CurrentWindow!.WindowStartUtc = DateTime.SpecifyKind(request.CurrentWindow.WindowStartUtc, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    private static WebhookFailureAnomalyDetectionRequestDto Request()
        => new()
        {
            CustomerId = "cust_123",
            CustomerIdType = "MDM",
            SubscriptionId = "sub_456",
            EndpointId = "endpoint_789",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            EventType = "OrderCreated",
            CurrentWindow = Window(),
            BaselineWindow = Window(new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc)),
            CreatedAtUtc = Now
        };

    private static WebhookFailureMetricWindowDto Window(DateTime? start = null)
    {
        var windowStart = start ?? new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc);
        return new WebhookFailureMetricWindowDto
        {
            WindowStartUtc = windowStart,
            WindowEndUtc = windowStart.AddMinutes(15),
            TotalDeliveries = 100,
            SuccessfulDeliveries = 90,
            FailedDeliveries = 10,
            RetryCount = 10,
            DeadLetterCount = 4,
            TimeoutCount = 4,
            RateLimitCount = 4,
            ClientErrorCount = 4,
            ServerErrorCount = 4,
            AuthenticationFailureCount = 4,
            AverageLatencyMs = 100,
            P95LatencyMs = 200
        };
    }

    private static void ApplySpike(WebhookFailureMetricWindowDto window, string metricName)
    {
        switch (metricName)
        {
            case "FailureRate": window.FailedDeliveries = 20; window.SuccessfulDeliveries = 80; break;
            case "RetryCount": window.RetryCount = 16; break;
            case "DeadLetterCount": window.DeadLetterCount = 5; break;
            case "TimeoutCount": window.TimeoutCount = 6; break;
            case "RateLimitCount": window.RateLimitCount = 6; break;
            case "ServerErrorCount": window.ServerErrorCount = 6; break;
            case "ClientErrorCount": window.ClientErrorCount = 6; break;
            case "AuthenticationFailureCount": window.AuthenticationFailureCount = 5; break;
            case "SignatureValidationFailureCount": window.SignatureValidationFailureCount = 1; break;
            case "SuspiciousPayloadCount": window.SuspiciousPayloadCount = 1; break;
            case "AverageLatencyMs": window.AverageLatencyMs = 160; break;
            case "P95LatencyMs": window.P95LatencyMs = 300; break;
        }
    }
}
