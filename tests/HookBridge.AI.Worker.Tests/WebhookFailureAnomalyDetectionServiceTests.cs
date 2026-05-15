using System.Text.Json;
using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailureAnomalyDetectionServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly WebhookFailureAnomalyDetectionService _service = new();

    [Fact]
    public void InterfaceContract_IsImplementedByWebhookFailureAnomalyDetectionService()
        => _service.Should().BeAssignableTo<IWebhookFailureAnomalyDetectionService>();

    [Fact]
    public void DetectAnomalies_WhenCurrentMetricsMatchBaseline_ReturnsNoAnomaly()
    {
        var response = _service.DetectAnomalies(Request("no-anomaly-current", "no-anomaly-baseline"), Now);

        response.IsAnomalyDetected.Should().BeFalse();
        response.AnomalyScore.Should().BeLessThan(25);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.DetectedAnomalies.Should().BeEmpty();
        response.Summary.Should().Contain("No webhook failure spike");
    }

    [Fact]
    public void DetectAnomalies_WhenFailureRateSpikes_DetectsFailureAnomaly()
    {
        var response = _service.DetectAnomalies(Request("failure-spike-current", "failure-spike-baseline"), Now);

        response.IsAnomalyDetected.Should().BeTrue();
        response.DetectedAnomalies.Should().Contain(anomaly =>
            anomaly.MetricName == "FailureRate" && anomaly.PercentageIncrease >= 50);
        response.Recommendation.Should().Contain("Review webhook failures");
        response.Recommendation.Should().Contain("retry strategy");
    }

    [Fact]
    public void DetectAnomalies_WhenRetryCountSpikes_DetectsRetryAnomalyWithScoreImpact()
    {
        var response = _service.DetectAnomalies(Request("retry-spike-current", "retry-spike-baseline"), Now);

        response.DetectedAnomalies.Should().Contain(anomaly =>
            anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.RetryCount) &&
            anomaly.PercentageIncrease >= 50 &&
            anomaly.ScoreImpact > 0);
    }

    [Fact]
    public void DetectAnomalies_WhenDeadLetterCountSpikes_RecommendsDlqReviewBeforeReplay()
    {
        var request = Request();
        request.CurrentWindow!.DeadLetterCount = 5;
        request.BaselineWindow!.DeadLetterCount = 4;

        var response = _service.DetectAnomalies(request, Now);

        response.DetectedAnomalies.Should().Contain(anomaly =>
            anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.DeadLetterCount) && anomaly.PercentageIncrease >= 25);
        response.Recommendation.Should().Contain("DLQ").And.Contain("before replay");
    }

    [Fact]
    public void DetectAnomalies_WhenTimeoutCountSpikes_RecommendsAvailabilityOrTimeoutInvestigation()
    {
        var response = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.TimeoutCount));

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.TimeoutCount));
        response.Recommendation.Should().Contain("receiver availability").And.Contain("timeout settings");
    }

    [Fact]
    public void DetectAnomalies_WhenHttp429Spikes_RecommendsBackoffAndReducedConcurrency()
    {
        var response = _service.DetectAnomalies(Request("rate-limit-spike-current", "rate-limit-spike-baseline"), Now);

        response.DetectedAnomalies.Should().Contain(anomaly =>
            anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.RateLimitCount) && anomaly.PercentageIncrease >= 50);
        response.Recommendation.Should().Contain("exponential backoff").And.Contain("reduce delivery concurrency");
    }

    [Fact]
    public void DetectAnomalies_WhenServerErrorsSpike_RecommendsReceiverHealthAndBackoff()
    {
        var response = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.ServerErrorCount));

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.ServerErrorCount));
        response.Recommendation.Should().Contain("receiver health").And.Contain("retry with backoff");
    }

    [Fact]
    public void DetectAnomalies_WhenClientErrorsSpike_RecommendsEndpointPayloadOrAuthReview()
    {
        var response = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.ClientErrorCount));

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.ClientErrorCount));
        response.Recommendation.Should().Contain("payload contract").And.Contain("endpoint URL").And.Contain("auth");
    }

    [Fact]
    public void DetectAnomalies_WhenAuthenticationFailuresSpike_IncreasesRiskAndMentionsCredentials()
    {
        var noAnomaly = _service.DetectAnomalies(Request(), Now);
        var response = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.AuthenticationFailureCount));

        ((int)response.RiskLevel).Should().BeGreaterThan((int)noAnomaly.RiskLevel);
        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.AuthenticationFailureCount));
        response.Recommendation.Should().Contain("credentials").And.Contain("token expiry");
    }

    [Fact]
    public void DetectAnomalies_WhenSignatureValidationFailuresAppear_RecommendsSigningSecretAndTolerance()
    {
        var request = Request();
        request.CurrentWindow!.SignatureValidationFailureCount = 1;
        request.BaselineWindow!.SignatureValidationFailureCount = 0;

        var response = _service.DetectAnomalies(request, Now);

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.SignatureValidationFailureCount));
        response.Recommendation.Should().Contain("signing secret").And.Contain("timestamp tolerance");
    }

    [Fact]
    public void DetectAnomalies_WhenSuspiciousPayloadsAppear_RecommendsManualSecurityReview()
    {
        var request = Request();
        request.CurrentWindow!.SuspiciousPayloadCount = 1;
        request.BaselineWindow!.SuspiciousPayloadCount = 0;

        var response = _service.DetectAnomalies(request, Now);

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.SuspiciousPayloadCount));
        response.Recommendation.Should().Contain("manual security review");
    }

    [Fact]
    public void DetectAnomalies_WhenLatencySpikes_DetectsAverageAndP95Latency()
    {
        var response = _service.DetectAnomalies(Request("latency-spike-current", "latency-spike-baseline"), Now);

        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.AverageLatencyMs) && anomaly.PercentageIncrease >= 50);
        response.DetectedAnomalies.Should().Contain(anomaly => anomaly.MetricName == nameof(WebhookFailureMetricWindowDto.P95LatencyMs) && anomaly.PercentageIncrease >= 50);
        response.Recommendation.Should().Contain("receiver performance");
    }

    [Fact]
    public void DetectAnomalies_ForExampleSpike_ReturnsHighRiskAndExpectedMetrics()
    {
        var request = Request("failure-spike-current", "failure-spike-baseline");
        request.CurrentWindow!.RateLimitCount = 50;
        request.CurrentWindow.RetryCount = 120;
        request.CurrentWindow.P95LatencyMs = 3500;
        request.BaselineWindow!.RateLimitCount = 5;
        request.BaselineWindow.RetryCount = 20;
        request.BaselineWindow.P95LatencyMs = 900;

        var response = _service.DetectAnomalies(request, Now);

        response.IsAnomalyDetected.Should().BeTrue();
        response.RiskLevel.Should().Be(AiRiskLevel.High);
        response.DetectedAnomalies.Select(anomaly => anomaly.MetricName).Should().Contain(new[]
        {
            "FailureRate",
            nameof(WebhookFailureMetricWindowDto.RateLimitCount),
            nameof(WebhookFailureMetricWindowDto.RetryCount),
            nameof(WebhookFailureMetricWindowDto.P95LatencyMs)
        });
        response.Recommendation.Should().Contain("exponential backoff").And.Contain("reduce delivery concurrency");
    }

    [Fact]
    public void DetectAnomalies_ClampsScoreAt100()
    {
        var request = Request();
        foreach (var metricName in SpikeMetricNames)
        {
            ApplySpike(request.CurrentWindow!, metricName);
        }

        var response = _service.DetectAnomalies(request, Now);

        response.AnomalyScore.Should().Be(100);
        response.AnomalyScore.Should().BeInRange(0, 100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
    }

    [Fact]
    public void DetectAnomalies_MultipleAnomaliesIncreaseTotalScore()
    {
        var retryOnly = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.RetryCount));
        var combined = Request();
        ApplySpike(combined.CurrentWindow!, nameof(WebhookFailureMetricWindowDto.RetryCount));
        ApplySpike(combined.CurrentWindow!, nameof(WebhookFailureMetricWindowDto.RateLimitCount));

        var combinedResponse = _service.DetectAnomalies(combined, Now);

        combinedResponse.AnomalyScore.Should().BeGreaterThan(retryOnly.AnomalyScore);
        combinedResponse.IsAnomalyDetected.Should().BeTrue();
    }

    [Fact]
    public void DetectAnomalies_SetsDetectionFlagAtThreshold()
    {
        var below = DetectSingleMetricSpike(nameof(WebhookFailureMetricWindowDto.RetryCount));
        below.AnomalyScore.Should().BeLessThan(25);
        below.IsAnomalyDetected.Should().BeFalse();

        var above = DetectSingleMetricSpike("FailureRate");
        above.AnomalyScore.Should().BeGreaterThanOrEqualTo(25);
        above.IsAnomalyDetected.Should().BeTrue();
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
    public void DetectAnomalies_WithMissingCurrentWindow_Throws()
    {
        var request = Request();
        request.CurrentWindow = null;

        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithMissingBaselineWindow_Throws()
    {
        var request = Request();
        request.BaselineWindow = null;

        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
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

    [Theory]
    [InlineData(nameof(WebhookFailureMetricWindowDto.TotalDeliveries))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.SuccessfulDeliveries))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.FailedDeliveries))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.RetryCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.DeadLetterCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.TimeoutCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.RateLimitCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.ClientErrorCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.ServerErrorCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.AuthenticationFailureCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.SignatureValidationFailureCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.SuspiciousPayloadCount))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.AverageLatencyMs))]
    [InlineData(nameof(WebhookFailureMetricWindowDto.P95LatencyMs))]
    public void DetectAnomalies_WithNegativeMetric_Throws(string propertyName)
    {
        var request = Request();
        SetMetric(request.CurrentWindow!, propertyName, -1);

        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Fact]
    public void DetectAnomalies_WithInvalidTargetUrl_Throws()
    {
        var request = Request();
        request.TargetUrl = "not-a-url";

        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, Now));
    }

    [Theory]
    [InlineData("current-start")]
    [InlineData("current-end")]
    [InlineData("baseline-start")]
    [InlineData("baseline-end")]
    [InlineData("created")]
    [InlineData("calculated")]
    public void DetectAnomalies_WithNonUtcDates_Throws(string dateName)
    {
        var request = Request();
        if (dateName == "current-start") request.CurrentWindow!.WindowStartUtc = DateTime.SpecifyKind(request.CurrentWindow.WindowStartUtc, DateTimeKind.Local);
        if (dateName == "current-end") request.CurrentWindow!.WindowEndUtc = DateTime.SpecifyKind(request.CurrentWindow.WindowEndUtc, DateTimeKind.Local);
        if (dateName == "baseline-start") request.BaselineWindow!.WindowStartUtc = DateTime.SpecifyKind(request.BaselineWindow.WindowStartUtc, DateTimeKind.Local);
        if (dateName == "baseline-end") request.BaselineWindow!.WindowEndUtc = DateTime.SpecifyKind(request.BaselineWindow.WindowEndUtc, DateTimeKind.Local);
        if (dateName == "created") request.CreatedAtUtc = DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Local);
        var calculatedAt = dateName == "calculated" ? DateTime.SpecifyKind(Now, DateTimeKind.Local) : Now;

        Assert.Throws<ArgumentException>(() => _service.DetectAnomalies(request, calculatedAt));
    }

    private WebhookFailureAnomalyDetectionResponseDto DetectSingleMetricSpike(string metricName)
    {
        var request = Request();
        ApplySpike(request.CurrentWindow!, metricName);
        return _service.DetectAnomalies(request, Now);
    }

    private static WebhookFailureAnomalyDetectionRequestDto Request(string? currentFixture = null, string? baselineFixture = null)
        => new()
        {
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            CustomerId = "cust_123",
            CustomerIdType = "MDM",
            SubscriptionId = "sub_456",
            EndpointId = "endpoint_789",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            EventType = "OrderCreated",
            CurrentWindow = currentFixture is null ? Window() : LoadWindow(currentFixture),
            BaselineWindow = baselineFixture is null ? Window(new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc)) : LoadWindow(baselineFixture),
            CreatedAtUtc = Now
        };

    private static WebhookFailureMetricWindowDto LoadWindow(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "AnomalyDetection", $"{fixtureName}.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WebhookFailureMetricWindowDto>(json, JsonOptions)!;
    }

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
            SignatureValidationFailureCount = 0,
            SuspiciousPayloadCount = 0,
            AverageLatencyMs = 100,
            P95LatencyMs = 200
        };
    }

    private static IReadOnlyList<string> SpikeMetricNames { get; } = new[]
    {
        "FailureRate",
        nameof(WebhookFailureMetricWindowDto.RetryCount),
        nameof(WebhookFailureMetricWindowDto.DeadLetterCount),
        nameof(WebhookFailureMetricWindowDto.TimeoutCount),
        nameof(WebhookFailureMetricWindowDto.RateLimitCount),
        nameof(WebhookFailureMetricWindowDto.ServerErrorCount),
        nameof(WebhookFailureMetricWindowDto.ClientErrorCount),
        nameof(WebhookFailureMetricWindowDto.AuthenticationFailureCount),
        nameof(WebhookFailureMetricWindowDto.SignatureValidationFailureCount),
        nameof(WebhookFailureMetricWindowDto.SuspiciousPayloadCount),
        nameof(WebhookFailureMetricWindowDto.AverageLatencyMs),
        nameof(WebhookFailureMetricWindowDto.P95LatencyMs)
    };

    private static void ApplySpike(WebhookFailureMetricWindowDto window, string metricName)
    {
        switch (metricName)
        {
            case "FailureRate": window.FailedDeliveries = 20; window.SuccessfulDeliveries = 80; break;
            case nameof(WebhookFailureMetricWindowDto.RetryCount): window.RetryCount = 16; break;
            case nameof(WebhookFailureMetricWindowDto.DeadLetterCount): window.DeadLetterCount = 5; break;
            case nameof(WebhookFailureMetricWindowDto.TimeoutCount): window.TimeoutCount = 6; break;
            case nameof(WebhookFailureMetricWindowDto.RateLimitCount): window.RateLimitCount = 6; break;
            case nameof(WebhookFailureMetricWindowDto.ServerErrorCount): window.ServerErrorCount = 6; break;
            case nameof(WebhookFailureMetricWindowDto.ClientErrorCount): window.ClientErrorCount = 6; break;
            case nameof(WebhookFailureMetricWindowDto.AuthenticationFailureCount): window.AuthenticationFailureCount = 5; break;
            case nameof(WebhookFailureMetricWindowDto.SignatureValidationFailureCount): window.SignatureValidationFailureCount = 1; break;
            case nameof(WebhookFailureMetricWindowDto.SuspiciousPayloadCount): window.SuspiciousPayloadCount = 1; break;
            case nameof(WebhookFailureMetricWindowDto.AverageLatencyMs): window.AverageLatencyMs = 160; break;
            case nameof(WebhookFailureMetricWindowDto.P95LatencyMs): window.P95LatencyMs = 300; break;
        }
    }

    private static void SetMetric(WebhookFailureMetricWindowDto window, string propertyName, double value)
    {
        var property = typeof(WebhookFailureMetricWindowDto).GetProperty(propertyName)!;
        if (property.PropertyType == typeof(int))
        {
            property.SetValue(window, (int)value);
            return;
        }

        property.SetValue(window, value);
    }
}
