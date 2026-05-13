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
