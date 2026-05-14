using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.Fallback;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiRetryRecommendationServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_WithValidAiJson_ParsesAndNormalizesResponse()
    {
        var service = CreateService(llmResponse: ValidAiJson(eventId: "ai-event", correlationId: "ai-corr"));
        var request = CreateRequest(statusCode: 500);

        var result = await service.AnalyzeAsync(request);

        result.EventId.Should().Be(request.EventId);
        result.CorrelationId.Should().Be(request.CorrelationId);
        result.AiSummary.Should().Be("The endpoint returned a transient server error.");
        result.RiskLevel.Should().Be(AiRiskLevel.Medium);
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.IsRetryRecommended.Should().BeTrue();
        result.Model.Should().Be("llama3-test");
        result.Provider.Should().Be("Ollama");
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidJson_UsesFallback()
    {
        var service = CreateService(llmResponse: "not json");

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.AiRecommendation.Should().Contain("AI response could not be used");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMissingRequiredField_UsesFallback()
    {
        var service = CreateService(llmResponse: """
        {
          "eventId": "evt-ai",
          "aiSummary": "summary",
          "aiRecommendation": "recommendation",
          "riskLevel": "Medium",
          "confidenceScore": 0.7,
          "suggestedRetryAction": "RetryWithBackoff",
          "isRetryRecommended": true,
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.AiRecommendation.Should().Contain("AI response could not be used");
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    public async Task AnalyzeAsync_WhenFallbackRetryableStatus_ReturnsRetryWithBackoff(int statusCode)
    {
        var service = CreateService(enabled: false);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: statusCode));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.IsRetryRecommended.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFallbackAuthenticationFailure_ReturnsRequireManualReview()
    {
        var service = CreateService(enabled: false);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 401));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RequireManualReview);
        result.IsRetryRecommended.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRetryCountReachedMax_ReturnsMoveToDeadLetter()
    {
        var service = CreateService(enabled: false);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500, retryCount: 5, maxRetryCount: 5));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.MoveToDeadLetter);
        result.IsRetryRecommended.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenAiDisabled_DoesNotCallLlmAndUsesFallback()
    {
        var llmClient = new TestLocalLlmClient(ValidAiJson(), shouldThrowIfCalled: true);
        var service = CreateService(enabled: false, llmClient: llmClient);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        llmClient.CallCount.Should().Be(0);
        result.AiRecommendation.Should().Contain("AI is disabled");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenLlmUnavailable_UsesFallback()
    {
        var service = CreateService(llmClient: new TestLocalLlmClient(new InvalidOperationException("offline")));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.AiRecommendation.Should().Contain("LLM analysis was unavailable");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenLlmReturnsEmptyResponse_UsesInvalidResponseFallbackMetadata()
    {
        var service = CreateService(llmResponse: string.Empty);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.Fallback.Should().NotBeNull();
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidResponse);
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenModelUnavailable_UsesFallbackMetadata()
    {
        var service = CreateService(llmClient: new TestLocalLlmClient(AiFallbackReason.ModelUnavailable, "Configured model is unavailable."));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.Fallback.Should().NotBeNull();
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.ModelUnavailable);
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenTimeout_UsesFallbackMetadata()
    {
        var service = CreateService(llmClient: new TestLocalLlmClient(AiFallbackReason.Timeout, "LLM timed out."));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 429));

        result.Fallback.Should().NotBeNull();
        result.Fallback!.FallbackReason.Should().Be(AiFallbackReason.Timeout);
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
    }

    [Fact]
    public async Task AnalyzeAsync_WithOutOfRangeConfidenceScore_ClampsValue()
    {
        var service = CreateService(llmResponse: ValidAiJson(confidenceScore: 1.7));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.ConfidenceScore.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_NormalizesEventIdAndCorrelationIdFromRequest()
    {
        var service = CreateService(llmResponse: ValidAiJson(eventId: "wrong-event", correlationId: "wrong-corr"));
        var request = CreateRequest(statusCode: 500);

        var result = await service.AnalyzeAsync(request);

        result.EventId.Should().Be(request.EventId);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task AnalyzeAsync_NormalizesGeneratedAtUtcKind()
    {
        var service = CreateService(llmResponse: ValidAiJson(generatedAtUtc: "2026-05-13T12:00:00"));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenAiSuggestsRetryImmediatelyFor429_OverridesToBackoff()
    {
        var service = CreateService(llmResponse: ValidAiJson(action: "RetryImmediately", isRetryRecommended: true));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 429));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff);
        result.IsRetryRecommended.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenAiSuggestsRetryAtMaxRetries_OverridesToDeadLetter()
    {
        var service = CreateService(llmResponse: ValidAiJson(action: "RetryImmediately", isRetryRecommended: true));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500, retryCount: 3, maxRetryCount: 3));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.MoveToDeadLetter);
        result.IsRetryRecommended.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidAiResponseShapes))]
    public async Task AnalyzeAsync_WithInvalidAiResponseShapes_UsesInvalidResponseFallback(
        string llmResponse,
        AiFallbackReason expectedReason)
    {
        var service = CreateService(llmResponse: llmResponse);

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.Fallback.Should().NotBeNull();
        result.Fallback!.FallbackReason.Should().Be(expectedReason);
        result.AiRecommendation.Should().Contain("AI response could not be used");
    }

    [Fact]
    public async Task AnalyzeAsync_WhenAiSuggestsRetryForForbiddenStatus_OverridesToManualReview()
    {
        var service = CreateService(llmResponse: ValidAiJson(action: "RetryWithBackoff", isRetryRecommended: true));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 403));

        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RequireManualReview);
        result.IsRetryRecommended.Should().BeFalse();
        result.AiRecommendation.Should().Contain("Authentication or authorization failures require manual review");
    }

    [Fact]
    public async Task AnalyzeAsync_PreservesModelAndProviderFromAiResponseWhenProvided()
    {
        var service = CreateService(llmResponse: ValidAiJson(model: "response-model", provider: "response-provider"));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.Model.Should().Be("response-model");
        result.Provider.Should().Be("response-provider");
    }

    [Fact]
    public async Task AnalyzeAsync_WithLocalGeneratedAtUtc_NormalizesToUtc()
    {
        var localTimestamp = new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Local).ToString("O");
        var service = CreateService(llmResponse: ValidAiJson(generatedAtUtc: localTimestamp));

        var result = await service.AnalyzeAsync(CreateRequest(statusCode: 500));

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static AiRetryRecommendationService CreateService(
        bool enabled = true,
        string? llmResponse = null,
        ILocalLlmClient? llmClient = null)
    {
        var options = Options.Create(new AiOptions
        {
            Enabled = enabled,
            Provider = "Ollama",
            Model = "llama3-test",
            Endpoint = "http://localhost:11434"
        });

        return new AiRetryRecommendationService(
            options,
            new TestPromptBuilder(),
            llmClient ?? new TestLocalLlmClient(llmResponse ?? ValidAiJson()),
            new AiFallbackService(options, new EndpointHealthScoringService(), new TestLogger<AiFallbackService>()),
            new TestLogger<AiRetryRecommendationService>());
    }

    private static WebhookFailureAnalysisRequestDto CreateRequest(
        int? statusCode,
        int retryCount = 0,
        int maxRetryCount = 3)
        => new()
        {
            EventId = "evt-request",
            CorrelationId = "corr-request",
            Source = "unit-test",
            EventType = "webhook.delivery.failed",
            StatusCode = statusCode,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount,
            FailureReason = statusCode is null ? "unknown" : $"HTTP {statusCode}",
            FailedAtUtc = DateTime.UtcNow
        };

    public static IEnumerable<object[]> InvalidAiResponseShapes()
    {
        yield return new object[] { "[]", AiFallbackReason.InvalidResponse };
        yield return new object[] { """
        {
          "eventId": "evt-ai",
          "aiSummary": "   ",
          "rootCause": "cause",
          "aiRecommendation": "recommendation",
          "riskLevel": "Medium",
          "confidenceScore": 0.7,
          "suggestedRetryAction": "RetryWithBackoff",
          "isRetryRecommended": true,
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """, AiFallbackReason.InvalidResponse };
        yield return new object[] { """
        {
          "eventId": "evt-ai",
          "aiSummary": "summary",
          "rootCause": "cause",
          "aiRecommendation": "recommendation",
          "riskLevel": "Severe",
          "confidenceScore": 0.7,
          "suggestedRetryAction": "RetryWithBackoff",
          "isRetryRecommended": true,
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """, AiFallbackReason.InvalidResponse };
        yield return new object[] { """
        {
          "eventId": "evt-ai",
          "aiSummary": "summary",
          "rootCause": "cause",
          "aiRecommendation": "recommendation",
          "riskLevel": "Medium",
          "confidenceScore": 0.7,
          "suggestedRetryAction": "RetryEventually",
          "isRetryRecommended": true,
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """, AiFallbackReason.InvalidResponse };
        yield return new object[] { """
        {
          "eventId": "evt-ai",
          "aiSummary": "summary",
          "rootCause": "cause",
          "aiRecommendation": "recommendation",
          "riskLevel": "Medium",
          "confidenceScore": 1e9999,
          "suggestedRetryAction": "RetryWithBackoff",
          "isRetryRecommended": true,
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """, AiFallbackReason.InvalidResponse };
        yield return new object[] { """
        {
          "eventId": "evt-ai",
          "aiSummary": "summary",
          "rootCause": "cause",
          "aiRecommendation": "recommendation",
          "riskLevel": "Medium",
          "confidenceScore": 0.7,
          "suggestedRetryAction": "RetryWithBackoff",
          "isRetryRecommended": "yes",
          "generatedAtUtc": "2026-05-13T00:00:00Z"
        }
        """, AiFallbackReason.InvalidResponse };
    }

    private static string ValidAiJson(
        string eventId = "evt-ai",
        string? correlationId = "corr-ai",
        double confidenceScore = 0.73,
        string riskLevel = "Medium",
        string action = "RetryWithBackoff",
        bool isRetryRecommended = true,
        string generatedAtUtc = "2026-05-13T00:00:00Z",
        string model = "",
        string provider = "")
        => $$"""
        {
          "eventId": "{{eventId}}",
          "correlationId": {{(correlationId is null ? "null" : $"\"{correlationId}\"")}},
          "aiSummary": "The endpoint returned a transient server error.",
          "rootCause": "The target service returned HTTP 500.",
          "aiRecommendation": "Retry with exponential backoff and monitor endpoint health.",
          "riskLevel": "{{riskLevel}}",
          "confidenceScore": {{confidenceScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "suggestedRetryAction": "{{action}}",
          "isRetryRecommended": {{isRetryRecommended.ToString().ToLowerInvariant()}},
          "generatedAtUtc": "{{generatedAtUtc}}",
          "model": "{{model}}",
          "provider": "{{provider}}"
        }
        """;

    private sealed class TestPromptBuilder : IWebhookFailurePromptBuilder
    {
        public string BuildPrompt(WebhookFailureAnalysisRequestDto request) => "prompt";
    }

    private sealed class TestLocalLlmClient : ILocalLlmClient
    {
        private readonly string? _response;
        private readonly Exception? _exception;
        private readonly bool _shouldThrowIfCalled;
        private readonly AiFallbackReason? _failureReason;

        public int CallCount { get; private set; }

        public TestLocalLlmClient(string response, bool shouldThrowIfCalled = false)
        {
            _response = response;
            _shouldThrowIfCalled = shouldThrowIfCalled;
        }

        public TestLocalLlmClient(Exception exception)
        {
            _exception = exception;
        }

        public TestLocalLlmClient(AiFallbackReason failureReason, string errorMessage)
        {
            _failureReason = failureReason;
            _response = errorMessage;
        }

        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (_shouldThrowIfCalled)
            {
                throw new InvalidOperationException("LLM should not have been called.");
            }

            if (_exception is not null)
            {
                return Task.FromResult(LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "LLM analysis was unavailable", 1));
            }

            if (_failureReason.HasValue)
            {
                return Task.FromResult(LlmResponseResult.Failure(_failureReason.Value, _response ?? "LLM failed", 1));
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(_response)
                ? LlmResponseResult.Failure(AiFallbackReason.InvalidResponse, "empty response", 0)
                : LlmResponseResult.Success(_response, 1));
        }
    }
}
