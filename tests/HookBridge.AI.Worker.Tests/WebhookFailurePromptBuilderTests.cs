using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailurePromptBuilderTests
{
    [Fact]
    public void BuildPrompt_ContainsRequiredWebhookFields()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("statusCode");
        prompt.Should().Contain("429");
        prompt.Should().Contain("errorMessage");
        prompt.Should().Contain("Too Many Requests");
        prompt.Should().Contain("failureReason");
        prompt.Should().Contain("Rate limit exceeded");
        prompt.Should().Contain("retryCount");
        prompt.Should().Contain("targetUrl");
        prompt.Should().Contain("https://example.com/webhooks");
        prompt.Should().Contain("eventType");
        prompt.Should().Contain("requestHeaders");
        prompt.Should().Contain("responseHeaders");
        prompt.Should().Contain("requestPayload");
        prompt.Should().Contain("responseBody");
        prompt.Should().Contain("Whether retry is safe");
        prompt.Should().Contain("Whether manual review is required");
    }

    [Fact]
    public void BuildPrompt_RequestsStrictJsonOutput()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("Return strict JSON only");
        prompt.Should().Contain("Do not include markdown, prose, comments, or code fences");
        prompt.Should().Contain("\"eventId\"");
        prompt.Should().Contain("\"correlationId\"");
        prompt.Should().Contain("\"aiSummary\"");
        prompt.Should().Contain("\"rootCause\"");
        prompt.Should().Contain("\"aiRecommendation\"");
        prompt.Should().Contain("\"riskLevel\"");
        prompt.Should().Contain("\"confidenceScore\"");
        prompt.Should().Contain("\"suggestedRetryAction\"");
        prompt.Should().Contain("\"isRetryRecommended\"");
        prompt.Should().Contain("\"generatedAtUtc\"");
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveHeaders()
    {
        var request = CreateRequest();
        request.RequestHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token",
            ["Cookie"] = "session=secret",
            ["Set-Cookie"] = "session=secret",
            ["X-API-Key"] = "api-key-secret",
            ["Api-Key"] = "api-key-secret",
            ["X-Custom-Token"] = "token-secret",
            ["X-Client-Secret"] = "client-secret",
            ["Password"] = "password-secret",
            ["X-Trace-Id"] = "trace-123"
        };

        var prompt = CreateBuilder().BuildPrompt(request);

        prompt.Should().Contain("[MASKED]");
        prompt.Should().Contain("trace-123");
        prompt.Should().NotContain("Bearer secret-token");
        prompt.Should().NotContain("session=secret");
        prompt.Should().NotContain("api-key-secret");
        prompt.Should().NotContain("token-secret");
        prompt.Should().NotContain("client-secret");
        prompt.Should().NotContain("password-secret");
    }

    [Fact]
    public void BuildPrompt_TruncatesLargeRequestPayload()
    {
        var request = CreateRequest();
        request.RequestPayload = new string('a', 25);

        var prompt = CreateBuilder(maxPromptPayloadLength: 10).BuildPrompt(request);

        prompt.Should().Contain("aaaaaaaaaa... [truncated from 25 to 10 characters]");
        prompt.Should().NotContain(new string('a', 25));
    }

    [Fact]
    public void BuildPrompt_TruncatesLargeResponseBody()
    {
        var request = CreateRequest();
        request.ResponseBody = new string('b', 30);

        var prompt = CreateBuilder(maxPromptPayloadLength: 12).BuildPrompt(request);

        prompt.Should().Contain("bbbbbbbbbbbb... [truncated from 30 to 12 characters]");
        prompt.Should().NotContain(new string('b', 30));
    }

    [Fact]
    public void BuildPrompt_HandlesNullOptionalFieldsSafely()
    {
        var request = new WebhookFailureAnalysisRequestDto
        {
            EventId = "evt-null",
            EventType = "webhook.delivery.failed",
            FailedAtUtc = DateTime.UnixEpoch
        };

        var prompt = CreateBuilder().BuildPrompt(request);

        prompt.Should().Contain("[not provided]");
    }

    [Fact]
    public void BuildPrompt_IncludesRiskLevelEnumValues()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("Unknown, Low, Medium, High, Critical");
        prompt.Should().Contain("Unknown|Low|Medium|High|Critical");
    }

    [Fact]
    public void BuildPrompt_IncludesSuggestedRetryActionEnumValues()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("None, RetryImmediately, RetryWithBackoff, MoveToDeadLetter, PauseEndpoint, RequireManualReview");
        prompt.Should().Contain("None|RetryImmediately|RetryWithBackoff|MoveToDeadLetter|PauseEndpoint|RequireManualReview");
    }


    [Fact]
    public void AddAiPromptServices_RegistersPromptBuilder()
    {
        var services = new ServiceCollection();
        services.AddAiOptions(new ConfigurationBuilder().Build());
        services.AddAiPromptServices();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWebhookFailurePromptBuilder>()
            .Should().BeOfType<WebhookFailurePromptBuilder>();
    }

    private static WebhookFailurePromptBuilder CreateBuilder(
        int maxPromptPayloadLength = 4000,
        bool maskSensitiveValues = true)
    {
        return new WebhookFailurePromptBuilder(Options.Create(new AiOptions
        {
            MaxPromptPayloadLength = maxPromptPayloadLength,
            MaskSensitiveValues = maskSensitiveValues
        }));
    }

    private static WebhookFailureAnalysisRequestDto CreateRequest()
    {
        return new WebhookFailureAnalysisRequestDto
        {
            EventId = "evt_123",
            CorrelationId = "corr_123",
            SubscriptionId = "sub_123",
            CustomerId = "cust_123",
            CustomerIdType = "tenant",
            EventType = "webhook.delivery.failed",
            Source = "hookbridge.worker",
            TargetUrl = "https://example.com/webhooks",
            HttpMethod = "POST",
            StatusCode = 429,
            ErrorMessage = "Too Many Requests",
            FailureReason = "Rate limit exceeded",
            RetryCount = 3,
            MaxRetryCount = 5,
            RequestHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            ResponseHeaders = new Dictionary<string, string>
            {
                ["Retry-After"] = "30"
            },
            RequestPayload = "{\"orderId\":\"ord_123\"}",
            ResponseBody = "Too Many Requests",
            FailedAtUtc = new DateTime(2026, 5, 13, 10, 15, 30, DateTimeKind.Utc)
        };
    }
}
