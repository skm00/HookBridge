using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using HookBridge.AI.Worker.Services.Fallback;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiFallbackServiceTests
{
    [Theory]
    [InlineData(429, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.Medium, true)]
    [InlineData(500, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.High, true)]
    [InlineData(401, SuggestedRetryAction.RequireManualReview, AiRiskLevel.High, false)]
    public async Task CreateRetryRecommendationAsync_UsesDeterministicStatusRules(
        int statusCode,
        SuggestedRetryAction expectedAction,
        AiRiskLevel expectedRisk,
        bool expectedRetry)
    {
        var service = CreateService();

        var result = await service.CreateRetryRecommendationAsync(
            CreateFailureRequest(statusCode),
            AiFallbackReason.ProviderUnavailable,
            "LLM provider was unavailable.");

        result.SuggestedRetryAction.Should().Be(expectedAction);
        result.RiskLevel.Should().Be(expectedRisk);
        result.IsRetryRecommended.Should().Be(expectedRetry);
        result.Fallback.Should().NotBeNull();
        result.Fallback!.UsedFallback.Should().BeTrue();
        result.Fallback.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
    }

    [Fact]
    public async Task CreateRetryRecommendationAsync_WhenMaxRetryReached_MovesToDeadLetterWithCriticalRisk()
    {
        var service = CreateService();

        var result = await service.CreateRetryRecommendationAsync(
            CreateFailureRequest(500, retryCount: 5, maxRetryCount: 5),
            AiFallbackReason.Timeout,
            "LLM provider timed out.");

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.MoveToDeadLetter);
        result.RiskLevel.Should().Be(AiRiskLevel.Critical);
        result.IsRetryRecommended.Should().BeFalse();
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.Timeout);
    }

    [Fact]
    public async Task CreateLogSummaryAsync_WithEmptyLogs_ReturnsSafeNoLogsMessage()
    {
        var service = CreateService();

        var result = await service.CreateLogSummaryAsync(
            new AiLogSummaryRequestDto { EventId = "evt-empty", Logs = Array.Empty<AiLogEntryDto>() },
            AiFallbackReason.AiDisabled,
            "AI is disabled.");

        result.Summary.Should().Contain("No logs are available");
        result.RiskLevel.Should().Be(AiRiskLevel.Low);
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
    }

    [Fact]
    public async Task CreateLogSummaryAsync_WithLatestError_SanitizesSensitiveValues()
    {
        var logger = new TestLogger<AiFallbackService>();
        var service = CreateService(logger);

        var result = await service.CreateLogSummaryAsync(
            new AiLogSummaryRequestDto
            {
                EventId = "evt-error",
                CorrelationId = "corr-error",
                Logs =
                [
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
                        Level = "Error",
                        Message = "Failed with Authorization: Bearer super-secret-token",
                    }
                ]
            },
            AiFallbackReason.InvalidJson,
            "AI returned invalid JSON.");

        result.RootCause.Should().Contain("[MASKED]");
        result.RootCause.Should().NotContain("super-secret-token");
        logger.Records.Select(record => record.Message).Should().NotContain(message => message.Contains("super-secret-token", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateRetryRecommendationAsync_PopulatesUtcFallbackMetadata()
    {
        var service = CreateService();

        var result = await service.CreateRetryRecommendationAsync(
            CreateFailureRequest(429),
            AiFallbackReason.ModelUnavailable,
            "Configured model is unavailable.");

        result.Fallback.Should().NotBeNull();
        result.Fallback!.Provider.Should().Be("Ollama");
        result.Fallback.Model.Should().Be("llama3-test");
        result.Fallback.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(408, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.Medium, 0.65)]
    [InlineData(504, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.Medium, 0.65)]
    [InlineData(502, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.High, 0.65)]
    [InlineData(503, SuggestedRetryAction.RetryWithBackoff, AiRiskLevel.High, 0.65)]
    [InlineData(400, SuggestedRetryAction.RequireManualReview, AiRiskLevel.Medium, 0.65)]
    [InlineData(403, SuggestedRetryAction.RequireManualReview, AiRiskLevel.High, 0.65)]
    [InlineData(404, SuggestedRetryAction.MoveToDeadLetter, AiRiskLevel.High, 0.65)]
    [InlineData(null, SuggestedRetryAction.RequireManualReview, AiRiskLevel.Unknown, 0.35)]
    public async Task CreateRetryRecommendationAsync_CoversFallbackStatusBranches(
        int? statusCode,
        SuggestedRetryAction expectedAction,
        AiRiskLevel expectedRisk,
        double expectedConfidence)
    {
        var service = CreateService();

        var result = await service.CreateRetryRecommendationAsync(
            CreateFailureRequest(statusCode),
            AiFallbackReason.InvalidResponse,
            "AI response could not be used.");

        result.SuggestedRetryAction.Should().Be(expectedAction);
        result.RiskLevel.Should().Be(expectedRisk);
        result.ConfidenceScore.Should().Be(expectedConfidence);
        result.RootCause.Should().NotBeNullOrWhiteSpace();
        result.AiRecommendation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateRetryRecommendationAsync_WhenFallbackMessageIsBlank_UsesReasonSpecificDefault()
    {
        var service = CreateService();

        var result = await service.CreateWebhookFailureAnalysisAsync(
            CreateFailureRequest(500),
            AiFallbackReason.AiDisabled,
            "   ");

        result.AiSummary.Should().Contain("LLM provider could not be used (AiDisabled)");
        result.ConfidenceScore.Should().Be(0.7);
        result.Fallback!.UsedFallback.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLogSummaryAsync_WithWarningLogs_ReturnsWarningSummaryAndLowRisk()
    {
        var service = CreateService();

        var result = await service.CreateLogSummaryAsync(
            new AiLogSummaryRequestDto
            {
                EventId = "evt-warning",
                Logs =
                [
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Local),
                        Level = "Warning",
                        Message = "Receiver latency is elevated"
                    }
                ]
            },
            AiFallbackReason.Timeout,
            "LLM timed out.");

        result.Summary.Should().Contain("warning log");
        result.RootCause.Should().Contain("No error-level log entry");
        result.Impact.Should().Contain("No confirmed delivery failure");
        result.RiskLevel.Should().Be(AiRiskLevel.Low);
        result.ConfidenceScore.Should().Be(0.35);
    }

    [Fact]
    public async Task CreateLogSummaryAsync_WithMultipleErrors_ReturnsHighRiskAndLatestSanitizedError()
    {
        var service = CreateService();

        var result = await service.CreateLogSummaryAsync(
            new AiLogSummaryRequestDto
            {
                EventId = "evt-errors",
                Logs =
                [
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 9, 0, 0, DateTimeKind.Utc),
                        Level = "Error",
                        Message = "Old error Token=older-secret"
                    },
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Unspecified),
                        Level = "Critical",
                        Message = "Latest error Password: newest-secret"
                    },
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 10, 1, 0, DateTimeKind.Utc),
                        Level = "Fatal",
                        Message = "Fatal error Api-Key => final-secret"
                    }
                ]
            },
            AiFallbackReason.InvalidJson,
            "AI returned invalid JSON.");

        result.Summary.Should().Contain("3 error log");
        result.RootCause.Should().Contain("[MASKED]");
        result.RootCause.Should().NotContain("final-secret");
        result.RiskLevel.Should().Be(AiRiskLevel.High);
        result.Impact.Should().Contain("may be delayed or failed");
    }

    [Fact]
    public async Task CreateLogSummaryAsync_WithInfoOnlyLogs_ReturnsLowRiskNoIssueSummary()
    {
        var service = CreateService();

        var result = await service.CreateLogSummaryAsync(
            new AiLogSummaryRequestDto
            {
                EventId = "evt-info",
                Logs =
                [
                    new AiLogEntryDto
                    {
                        TimestampUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
                        Level = "Information",
                        Message = "Delivery completed"
                    }
                ]
            },
            AiFallbackReason.None,
            "No fallback needed.");

        result.Summary.Should().Contain("no error or warning");
        result.RootCause.Should().Contain("No error-level log entry");
        result.RiskLevel.Should().Be(AiRiskLevel.Low);
        result.Fallback!.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task CreateEndpointHealthSummaryAsync_AddsFallbackMetadataToHealthScore()
    {
        var service = CreateService();
        var now = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

        var result = await service.CreateEndpointHealthSummaryAsync(
            new EndpointHealthScoreRequestDto
            {
                EndpointId = "endpoint-1",
                SubscriptionId = "sub-1",
                CustomerId = "customer-1",
                TargetUrl = "https://example.test/webhooks",
                Environment = "test",
                TotalDeliveries = 10,
                SuccessfulDeliveries = 10,
                FailedDeliveries = 0,
                EvaluationWindowFromUtc = now.AddHours(-1),
                EvaluationWindowToUtc = now
            },
            AiFallbackReason.ProviderUnavailable,
            "Provider unavailable.");

        result.HealthStatus.Should().Be(EndpointHealthStatus.Healthy);
        result.Fallback.Should().NotBeNull();
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        result.Fallback.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static AiFallbackService CreateService(TestLogger<AiFallbackService>? logger = null)
    {
        var options = Options.Create(new AiOptions
        {
            Provider = "Ollama",
            Model = "llama3-test",
            MaxFallbackSummaryLength = 1000
        });

        return new AiFallbackService(
            options,
            new EndpointHealthScoringService(),
            logger ?? new TestLogger<AiFallbackService>());
    }

    private static WebhookFailureAnalysisRequestDto CreateFailureRequest(
        int? statusCode,
        int retryCount = 0,
        int maxRetryCount = 3)
        => new()
        {
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            EventType = "webhook.delivery.failed",
            StatusCode = statusCode,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount,
            FailedAtUtc = new DateTime(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc)
        };
}
