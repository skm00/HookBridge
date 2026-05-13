using System.Text.Json;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mapping;
using HookBridge.AI.Worker.Validation;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailureAnalysisDtoTests
{
    private readonly WebhookFailureAnalysisRequestDtoValidator _validator = new();

    [Fact]
    public void RequestValidator_WithValidRequest_Succeeds()
    {
        var request = CreateValidRequest();

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequestValidator_WhenEventIdMissing_Fails()
    {
        var request = CreateValidRequest();
        request.EventId = string.Empty;

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(WebhookFailureAnalysisRequestDto.EventId));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void RequestValidator_WhenStatusCodeInvalid_Fails(int statusCode)
    {
        var request = CreateValidRequest();
        request.StatusCode = statusCode;

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(WebhookFailureAnalysisRequestDto.StatusCode));
    }

    [Fact]
    public void RequestValidator_WhenRetryCountInvalid_Fails()
    {
        var request = CreateValidRequest();
        request.RetryCount = -1;

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(WebhookFailureAnalysisRequestDto.RetryCount));
    }

    [Fact]
    public void RequestValidator_WhenTargetUrlInvalid_Fails()
    {
        var request = CreateValidRequest();
        request.TargetUrl = "not-a-url";

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(WebhookFailureAnalysisRequestDto.TargetUrl));
    }

    [Fact]
    public void Mapper_FromAiAnalysisEventDto_CreatesWebhookFailureAnalysisRequest()
    {
        var createdAt = new DateTimeOffset(2026, 5, 13, 10, 15, 30, TimeSpan.Zero);
        var analysisEvent = new AiAnalysisEventDto
        {
            EventId = "evt-123",
            CorrelationId = "corr-123",
            Source = "hookbridge.worker",
            EventType = "webhook.delivery.failed",
            FailureReason = "HTTP 500",
            Payload = "{\"orderId\":\"ord-1\"}",
            CreatedAtUtc = createdAt
        };

        var request = WebhookFailureAnalysisMapper.ToWebhookFailureAnalysisRequest(analysisEvent);

        request.EventId.Should().Be("evt-123");
        request.CorrelationId.Should().Be("corr-123");
        request.Source.Should().Be("hookbridge.worker");
        request.EventType.Should().Be("webhook.delivery.failed");
        request.FailureReason.Should().Be("HTTP 500");
        request.RequestPayload.Should().Be("{\"orderId\":\"ord-1\"}");
        request.FailedAtUtc.Should().Be(createdAt.UtcDateTime);
        request.FailedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Mapper_FromWebhookFailureAnalysisResponseDto_CreatesAiAnalysisResult()
    {
        var generatedAt = DateTime.SpecifyKind(new DateTime(2026, 5, 13, 10, 15, 30), DateTimeKind.Utc);
        var response = new WebhookFailureAnalysisResponseDto
        {
            EventId = "evt-123",
            CorrelationId = "corr-123",
            AiSummary = "Endpoint returned a transient server error.",
            RootCause = "Target service outage.",
            AiRecommendation = "Retry with exponential backoff after checking endpoint health.",
            RiskLevel = AiRiskLevel.High,
            ConfidenceScore = 0.91,
            SuggestedRetryAction = SuggestedRetryAction.RetryWithBackoff,
            IsRetryRecommended = true,
            GeneratedAtUtc = generatedAt,
            Model = "llama3.1",
            Provider = "Ollama"
        };

        var result = WebhookFailureAnalysisMapper.ToAiAnalysisResult(response);

        result.EventId.Should().Be("evt-123");
        result.CorrelationId.Should().Be("corr-123");
        result.AiSummary.Should().Be(response.AiSummary);
        result.RootCause.Should().Be(response.RootCause);
        result.AiRecommendation.Should().Be(response.AiRecommendation);
        result.RiskLevel.Should().Be("High");
        result.ConfidenceScore.Should().Be(0.91);
        result.SuggestedRetryAction.Should().Be("RetryWithBackoff");
        result.IsRetryRecommended.Should().BeTrue();
        result.Model.Should().Be("llama3.1");
        result.Provider.Should().Be("Ollama");
        result.CreatedAtUtc.Should().Be(generatedAt);
        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Mapper_FromWebhookFailureAnalysisRequestDto_CreatesPlaceholderAiAnalysisResult()
    {
        var request = CreateValidRequest();
        var options = new AiOptions { Model = "llama3.1", Provider = "Ollama" };

        var result = WebhookFailureAnalysisMapper.ToAiAnalysisResultPlaceholder(request, options);

        result.EventId.Should().Be(request.EventId);
        result.CorrelationId.Should().Be(request.CorrelationId);
        result.Source.Should().Be(request.Source);
        result.EventType.Should().Be(request.EventType);
        result.FailureReason.Should().Be(request.FailureReason);
        result.AiSummary.Should().Contain(request.FailureReason!);
        result.RiskLevel.Should().Be(nameof(AiRiskLevel.Unknown));
        result.SuggestedRetryAction.Should().Be(nameof(SuggestedRetryAction.RequireManualReview));
        result.IsRetryRecommended.Should().BeFalse();
        result.Model.Should().Be("llama3.1");
        result.Provider.Should().Be("Ollama");
        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void WebhookFailureAnalysisResponseDto_SerializesEnumsAsStrings()
    {
        var response = new WebhookFailureAnalysisResponseDto
        {
            EventId = "evt-123",
            RiskLevel = AiRiskLevel.Critical,
            SuggestedRetryAction = SuggestedRetryAction.MoveToDeadLetter
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"riskLevel\":\"Critical\"");
        json.Should().Contain("\"suggestedRetryAction\":\"MoveToDeadLetter\"");
    }

    private static WebhookFailureAnalysisRequestDto CreateValidRequest()
        => new()
        {
            EventId = "evt-123",
            CorrelationId = "corr-123",
            SubscriptionId = "sub-123",
            CustomerId = "cust-123",
            CustomerIdType = "TenantId",
            EventType = "webhook.delivery.failed",
            Source = "hookbridge.worker",
            TargetUrl = "https://api.example.com/webhooks/orders",
            HttpMethod = "POST",
            StatusCode = 500,
            ErrorMessage = "Internal Server Error",
            FailureReason = "HTTP 500",
            RetryCount = 2,
            MaxRetryCount = 5,
            RequestHeaders = new Dictionary<string, string> { ["content-type"] = "application/json" },
            ResponseHeaders = new Dictionary<string, string> { ["retry-after"] = "30" },
            RequestPayload = "{\"orderId\":\"ord-1\"}",
            ResponseBody = "{\"error\":\"temporarily unavailable\"}",
            FailedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 13, 10, 15, 30), DateTimeKind.Utc)
        };
}
