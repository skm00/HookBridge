using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.Fallback;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiLogSummarizationServiceTests
{
    [Fact]
    public async Task SummarizeAsync_WithValidAiJson_ParsesAndNormalizesResponse()
    {
        var service = CreateService(llmResponse: ValidAiJson(eventId: "ai-event", correlationId: "ai-corr"));
        var request = CreateRequest();

        var result = await service.SummarizeAsync(request);

        result.EventId.Should().Be(request.EventId);
        result.CorrelationId.Should().Be(request.CorrelationId);
        result.Summary.Should().Be("Webhook delivery failed because the target endpoint returned HTTP 429.");
        result.RootCause.Should().Be("The receiver is rate limiting requests.");
        result.Impact.Should().Be("Webhook delivery may be delayed until retries succeed.");
        result.Recommendation.Should().Be("Retry with exponential backoff and reduce delivery concurrency for this endpoint.");
        result.RiskLevel.Should().Be(AiRiskLevel.Medium);
        result.ConfidenceScore.Should().Be(0.85);
        result.Model.Should().Be("llama3-test");
        result.Provider.Should().Be("Ollama");
    }

    [Fact]
    public async Task SummarizeAsync_WithInvalidJson_UsesFallback()
    {
        var service = CreateService(llmResponse: "not json");

        var result = await service.SummarizeAsync(CreateRequest());

        result.Summary.Should().Contain("1 error log(s)");
        result.RootCause.Should().Contain("HTTP 429");
        result.Recommendation.Should().Contain("AI response could not be used");
        result.ConfidenceScore.Should().BeLessThan(0.5);
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyLogs_UsesFallbackAndDoesNotCallLlm()
    {
        var llmClient = new TestLocalLlmClient(ValidAiJson(), shouldThrowIfCalled: true);
        var service = CreateService(llmClient: llmClient);
        var request = CreateRequest();
        request.Logs = [];

        var result = await service.SummarizeAsync(request);

        llmClient.CallCount.Should().Be(0);
        result.Summary.Should().Contain("No logs are available");
        result.RootCause.Should().Contain("No logs are available");
        result.ConfidenceScore.Should().Be(0.1);
    }

    [Fact]
    public async Task SummarizeAsync_WhenAiDisabled_UsesFallbackAndDoesNotCallLlm()
    {
        var llmClient = new TestLocalLlmClient(ValidAiJson(), shouldThrowIfCalled: true);
        var service = CreateService(enabled: false, llmClient: llmClient);

        var result = await service.SummarizeAsync(CreateRequest());

        llmClient.CallCount.Should().Be(0);
        result.Recommendation.Should().Contain("AI is disabled");
        result.ConfidenceScore.Should().BeLessThan(0.5);
    }

    [Fact]
    public async Task SummarizeAsync_WhenLlmUnavailable_UsesFallback()
    {
        var service = CreateService(llmClient: new TestLocalLlmClient(new InvalidOperationException("offline")));

        var result = await service.SummarizeAsync(CreateRequest());

        result.Recommendation.Should().Contain("LLM summarization was unavailable");
        result.RiskLevel.Should().Be(AiRiskLevel.Medium);
    }

    [Fact]
    public async Task SummarizeAsync_WithInvalidRiskLevel_UsesFallback()
    {
        var service = CreateService(llmResponse: ValidAiJson(riskLevel: "Severe"));

        var result = await service.SummarizeAsync(CreateRequest());

        result.Recommendation.Should().Contain("riskLevel is not a valid AiRiskLevel value");
        result.RiskLevel.Should().Be(AiRiskLevel.Medium);
    }

    [Theory]
    [InlineData(1.7, 1)]
    [InlineData(-0.2, 0)]
    public async Task SummarizeAsync_WithOutOfRangeConfidenceScore_ClampsValue(double confidenceScore, double expected)
    {
        var service = CreateService(llmResponse: ValidAiJson(confidenceScore: confidenceScore));

        var result = await service.SummarizeAsync(CreateRequest());

        result.ConfidenceScore.Should().Be(expected);
    }

    [Fact]
    public async Task SummarizeAsync_NormalizesEventIdAndCorrelationIdFromRequest()
    {
        var service = CreateService(llmResponse: ValidAiJson(eventId: "wrong-event", correlationId: "wrong-corr"));
        var request = CreateRequest();

        var result = await service.SummarizeAsync(request);

        result.EventId.Should().Be(request.EventId);
        result.CorrelationId.Should().Be(request.CorrelationId);
    }

    [Fact]
    public async Task SummarizeAsync_NormalizesGeneratedAtUtcKind()
    {
        var service = CreateService(llmResponse: ValidAiJson(generatedAtUtc: "2026-05-13T12:00:00"));

        var result = await service.SummarizeAsync(CreateRequest());

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AddAiLogSummarizationServices_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(new ConfigurationBuilder().Build());
        services.AddSingleton<IAiLogSummaryPromptBuilder, TestPromptBuilder>();
        services.AddSingleton<ILocalLlmClient>(_ => new TestLocalLlmClient(ValidAiJson()));
        services.AddAiLogSummarizationServices();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAiLogSummarizationService>()
            .Should().BeOfType<AiLogSummarizationService>();
    }

    private static AiLogSummarizationService CreateService(
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

        return new AiLogSummarizationService(
            options,
            new TestPromptBuilder(),
            llmClient ?? new TestLocalLlmClient(llmResponse ?? ValidAiJson()),
            new AiFallbackService(options, new EndpointHealthScoringService(), new TestLogger<AiFallbackService>()),
            new TestLogger<AiLogSummarizationService>());
    }

    private static AiLogSummaryRequestDto CreateRequest()
        => new()
        {
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            Source = "unit-test",
            Environment = "qa",
            FromUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 5, 13, 10, 15, 0, DateTimeKind.Utc),
            Logs =
            [
                new AiLogEntryDto
                {
                    TimestampUtc = new DateTime(2026, 5, 13, 10, 10, 0, DateTimeKind.Utc),
                    Level = "Error",
                    Message = "Webhook delivery failed with HTTP 429 Too Many Requests",
                    ServiceName = "HookBridge.Worker"
                },
                new AiLogEntryDto
                {
                    TimestampUtc = new DateTime(2026, 5, 13, 10, 9, 0, DateTimeKind.Utc),
                    Level = "Warning",
                    Message = "Retry budget nearing limit",
                    ServiceName = "HookBridge.Worker"
                }
            ]
        };

    private static string ValidAiJson(
        string eventId = "evt-ai",
        string? correlationId = "corr-ai",
        double confidenceScore = 0.85,
        string riskLevel = "Medium",
        string generatedAtUtc = "2026-05-13T10:15:00Z")
        => $$"""
        {
          "eventId": "{{eventId}}",
          "correlationId": {{(correlationId is null ? "null" : $"\"{correlationId}\"")}},
          "summary": "Webhook delivery failed because the target endpoint returned HTTP 429.",
          "rootCause": "The receiver is rate limiting requests.",
          "impact": "Webhook delivery may be delayed until retries succeed.",
          "recommendation": "Retry with exponential backoff and reduce delivery concurrency for this endpoint.",
          "riskLevel": "{{riskLevel}}",
          "confidenceScore": {{confidenceScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "generatedAtUtc": "{{generatedAtUtc}}"
        }
        """;

    private sealed class TestPromptBuilder : IAiLogSummaryPromptBuilder
    {
        public string BuildPrompt(AiLogSummaryRequestDto request) => "prompt";
    }

    private sealed class TestLocalLlmClient : ILocalLlmClient
    {
        private readonly string? _response;
        private readonly Exception? _exception;
        private readonly bool _shouldThrowIfCalled;

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

        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (_shouldThrowIfCalled)
            {
                throw new InvalidOperationException("LLM should not have been called.");
            }

            if (_exception is not null)
            {
                return Task.FromResult(LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "LLM summarization was unavailable", 1));
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(_response)
                ? LlmResponseResult.Failure(AiFallbackReason.InvalidResponse, "empty response", 0)
                : LlmResponseResult.Success(_response, 1));
        }
    }
}
