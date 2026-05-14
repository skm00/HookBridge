using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services;
using HookBridge.Api.Configuration;
using HookBridge.Api.Controllers;
using HookBridge.Api.Services.AiNaturalLanguageQuery;
using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using HookBridge.Application.Interfaces;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.Api.Tests;

public sealed class AiNaturalLanguageQueryServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueryValidationSuccess_ReturnsResponse()
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "Why are failures happening?" });

        Assert.Equal(AiNaturalLanguageQueryIntent.FailureAnalysis, response.Intent);
        Assert.NotEmpty(response.Answer);
    }

    [Fact]
    public async Task EmptyQueryValidationFailure_Throws()
    {
        await Assert.ThrowsAsync<AiNaturalLanguageQueryValidationException>(() => CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = " " }));
    }

    [Fact]
    public async Task QueryTooLongValidationFailure_Throws()
    {
        await Assert.ThrowsAsync<AiNaturalLanguageQueryValidationException>(() => CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = new string('x', 1001) }));
    }

    [Fact]
    public async Task DefaultDateRange_UsesLast24Hours()
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.Equal(NowUtc.AddHours(-24), response.FiltersUsed["fromUtc"]);
        Assert.Equal(NowUtc, response.FiltersUsed["toUtc"]);
    }

    [Fact]
    public async Task InvalidDateRange_Throws()
    {
        await Assert.ThrowsAsync<AiNaturalLanguageQueryValidationException>(() => CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto
        {
            Query = "show failures",
            FromUtc = NowUtc,
            ToUtc = NowUtc.AddMinutes(-1)
        }));
    }

    [Fact]
    public async Task DateRangeOverMaxLimit_Throws()
    {
        await Assert.ThrowsAsync<AiNaturalLanguageQueryValidationException>(() => CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto
        {
            Query = "show failures",
            FromUtc = NowUtc.AddDays(-91),
            ToUtc = NowUtc
        }));
    }

    [Fact]
    public async Task MaxResultsDefault_IsApplied()
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.Equal(20, response.FiltersUsed["maxResults"]);
    }

    [Fact]
    public async Task MaxResultsCappedAtHardMax()
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures", MaxResults = 500 });

        Assert.Equal(100, response.FiltersUsed["maxResults"]);
    }

    [Theory]
    [InlineData("show failures", AiNaturalLanguageQueryIntent.FailureAnalysis)]
    [InlineData("anomaly spike today", AiNaturalLanguageQueryIntent.AnomalySearch)]
    [InlineData("security attack", AiNaturalLanguageQueryIntent.SecurityFindings)]
    [InlineData("what should I retry", AiNaturalLanguageQueryIntent.RetryRecommendations)]
    [InlineData("show risk", AiNaturalLanguageQueryIntent.EndpointRisk)]
    [InlineData("endpoint health", AiNaturalLanguageQueryIntent.EndpointHealth)]
    [InlineData("which events go to dlq", AiNaturalLanguageQueryIntent.DeadLetterRecommendations)]
    public async Task IntentDetection_DetectsExpectedIntent(string query, AiNaturalLanguageQueryIntent expected)
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = query });

        Assert.Equal(expected, response.Intent);
    }

    [Fact]
    public async Task EventLookupIntentDetection_WhenEventIdPresent()
    {
        var response = await CreateService().QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "why did event fail", EventId = "evt_123" });

        Assert.Equal(AiNaturalLanguageQueryIntent.EventLookup, response.Intent);
    }

    [Fact]
    public async Task AiAnswerParsingSuccess_UsesAiAnswer()
    {
        var llm = new FakeLlmClient("{\"answer\":\"AI says retry later.\",\"suggestedActions\":[\"Retry with backoff.\"],\"confidenceScore\":0.91}");
        var response = await CreateService(llmClient: llm).QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.Equal("AI says retry later.", response.Answer);
        Assert.False(response.Fallback);
        Assert.Equal(0.91, response.ConfidenceScore);
    }

    [Fact]
    public async Task InvalidAiJsonFallback_UsesFallback()
    {
        var response = await CreateService(llmClient: new FakeLlmClient("not-json")).QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.True(response.Fallback);
        Assert.Contains("Found", response.Answer);
    }

    [Fact]
    public async Task AiDisabledFallback_UsesFallback()
    {
        var response = await CreateService(aiEnabled: false).QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.True(response.Fallback);
    }

    [Fact]
    public async Task NoResultsFallback_SaysNoMatchingDataFound()
    {
        var response = await CreateService(empty: true).QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" });

        Assert.True(response.Fallback);
        Assert.Contains("No matching", response.Answer);
    }

    [Fact]
    public async Task ControllerReturns200Ok()
    {
        var controller = new AiNaturalLanguageQueryController(new StubQueryService(), NullLogger<AiNaturalLanguageQueryController>.Instance);

        var action = await controller.QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.IsType<ApiResponse<AiNaturalLanguageQueryResponseDto>>(ok.Value);
    }

    [Fact]
    public async Task ControllerReturns400BadRequest()
    {
        var controller = new AiNaturalLanguageQueryController(new ThrowingQueryService(new AiNaturalLanguageQueryValidationException("Query", "Query is required.")), NullLogger<AiNaturalLanguageQueryController>.Instance);

        var action = await controller.QueryAsync(new AiNaturalLanguageQueryRequestDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task ControllerReturns500OnUnexpectedException()
    {
        var controller = new AiNaturalLanguageQueryController(new ThrowingQueryService(new InvalidOperationException("boom")), NullLogger<AiNaturalLanguageQueryController>.Instance);

        var action = await controller.QueryAsync(new AiNaturalLanguageQueryRequestDto { Query = "show failures" }, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    [Fact]
    public void RequiredServicesAreRegisteredInDi()
    {
        var services = new ServiceCollection();
        services.AddOptions<AiNaturalLanguageQueryOptions>();
        services.AddOptions<AiOptions>();
        services.AddSingleton<IDateTimeProvider>(new FakeDateTimeProvider());
        services.AddSingleton<ILocalLlmClient>(new FakeLlmClient("{\"answer\":\"ok\",\"suggestedActions\":[],\"confidenceScore\":0.5}"));
        services.AddSingleton<IAiNaturalLanguageQueryPromptBuilder, AiNaturalLanguageQueryPromptBuilder>();
        services.AddSingleton<IAiNaturalLanguageQueryService, AiNaturalLanguageQueryService>();
        services.AddSingleton<IAiAnalysisResultRepository>(new FakeAnalysisRepository(false));
        services.AddSingleton<IAiAnomalyRecordRepository>(new FakeAnomalyRepository(false));
        services.AddSingleton<IAiSecurityAnalysisRepository>(new FakeSecurityRepository(false));
        services.AddSingleton<ICustomerEndpointRiskScoreRepository>(new FakeRiskRepository(false));
        services.AddSingleton<IWebhookFailureAnomalyDetectionRepository>(new FakeFailureAnomalyRepository(false));
        services.AddLogging();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAiNaturalLanguageQueryService>());
        Assert.NotNull(provider.GetRequiredService<IAiNaturalLanguageQueryPromptBuilder>());
        Assert.NotNull(provider.GetRequiredService<IOptions<AiNaturalLanguageQueryOptions>>());
    }

    private static AiNaturalLanguageQueryService CreateService(ILocalLlmClient? llmClient = null, bool aiEnabled = true, bool empty = false)
    {
        return new AiNaturalLanguageQueryService(
            new FakeAnalysisRepository(empty),
            new FakeAnomalyRepository(empty),
            new FakeSecurityRepository(empty),
            new FakeRiskRepository(empty),
            new FakeFailureAnomalyRepository(empty),
            new AiNaturalLanguageQueryPromptBuilder(),
            llmClient ?? new FakeLlmClient("{\"answer\":\"AI answer.\",\"suggestedActions\":[\"Review result.\"],\"confidenceScore\":0.8}"),
            Options.Create(new AiNaturalLanguageQueryOptions()),
            Options.Create(new AiOptions { Enabled = aiEnabled, Provider = "Ollama", Model = "llama3" }),
            new FakeDateTimeProvider(),
            NullLogger<AiNaturalLanguageQueryService>.Instance);
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => NowUtc;
    }

    private sealed class FakeLlmClient(string responseText) : ILocalLlmClient
    {
        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(LlmResponseResult.Success(responseText, 1));
    }

    private sealed class StubQueryService : IAiNaturalLanguageQueryService
    {
        public Task<AiNaturalLanguageQueryResponseDto> QueryAsync(AiNaturalLanguageQueryRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiNaturalLanguageQueryResponseDto { Query = request.Query, Answer = "ok", GeneratedAtUtc = NowUtc });
    }

    private sealed class ThrowingQueryService(Exception exception) : IAiNaturalLanguageQueryService
    {
        public Task<AiNaturalLanguageQueryResponseDto> QueryAsync(AiNaturalLanguageQueryRequestDto request, CancellationToken cancellationToken = default)
            => throw exception;
    }

    private static AiAnalysisResult Analysis(string eventId = "evt_123", string? correlationId = "corr_123") => new() { Id = "analysis_1", EventId = eventId, CorrelationId = correlationId, CustomerId = "cust_123", CustomerIdType = "external", SubscriptionId = "sub_123", EndpointId = "end_123", Environment = "qa", FailureReason = "HTTP 429", AiSummary = "Target endpoint returned Too Many Requests.", RiskLevel = "Medium", SuggestedRetryAction = "RetryWithBackoff", CreatedAtUtc = NowUtc.AddMinutes(-10) };
    private static AiAnomalyRecord Anomaly(string? eventId = "evt_123", string? correlationId = "corr_123", string customerId = "cust_123", string? subscriptionId = "sub_123", string? endpointId = "end_123") => new() { Id = "anom_1", AnomalyId = "anom_1", EventId = eventId, CorrelationId = correlationId, CustomerId = customerId, CustomerIdType = "external", SubscriptionId = subscriptionId, EndpointId = endpointId, Environment = "qa", AnomalyType = "RateLimitSpike", Summary = "Rate limit failures spiked.", Recommendation = "Reduce concurrency.", RiskLevel = "High", CreatedAtUtc = NowUtc.AddMinutes(-5) };
    private static AiSecurityAnalysisResult Security(string eventId = "evt_123", string? correlationId = "corr_123", string customerId = "cust_123") => new() { Id = "sec_1", EventId = eventId, CorrelationId = correlationId, CustomerId = customerId, CustomerIdType = "external", SubscriptionId = "sub_123", EndpointId = "end_123", Environment = "qa", IsSuspicious = true, Summary = "Suspicious authentication failures.", SuggestedAction = "Investigate", RiskLevel = "High", CreatedAtUtc = NowUtc.AddMinutes(-7) };
    private static CustomerEndpointRiskScoreResult Risk(string customerId = "cust_123", string? subscriptionId = "sub_123", string? endpointId = "end_123") => new() { Id = "risk_1", CustomerId = customerId, CustomerIdType = "external", SubscriptionId = subscriptionId, EndpointId = endpointId, Environment = "qa", Summary = "Endpoint is high risk.", Recommendation = "Throttle delivery.", RiskLevel = "High", HealthStatus = "Degraded", CreatedAtUtc = NowUtc.AddMinutes(-8) };
    private static WebhookFailureAnomalyDetectionResult FailureAnomaly(string customerId = "cust_123", string? subscriptionId = "sub_123", string? endpointId = "end_123") => new() { Id = "fail_anom_1", CustomerId = customerId, CustomerIdType = "external", SubscriptionId = subscriptionId, EndpointId = endpointId, Environment = "qa", IsAnomalyDetected = true, Summary = "Failure volume is anomalous.", Recommendation = "Review endpoint.", RiskLevel = "High", CreatedAtUtc = NowUtc.AddMinutes(-9) };

    private sealed class FakeAnalysisRepository(bool empty) : IAiAnalysisResultRepository
    {
        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(empty ? null : Analysis());
        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(empty ? null : Analysis(eventId));
        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(empty ? [] : [Analysis(correlationId: correlationId)]);
        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(empty ? [] : [Analysis()]);
    }

    private sealed class FakeAnomalyRepository(bool empty) : IAiAnomalyRecordRepository
    {
        public Task<AiAnomalyRecordRepositoryResult> InsertAsync(AiAnomalyRecord record, CancellationToken cancellationToken = default) => Task.FromResult(AiAnomalyRecordRepositoryResult.Success(record));
        public Task<AiAnomalyRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(empty ? null : Anomaly());
        public Task<AiAnomalyRecord?> GetByAnomalyIdAsync(string anomalyId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(empty ? null : Anomaly());
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(eventId)]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(correlationId: correlationId)]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(customerId: customerId)]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(subscriptionId: subscriptionId)]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(endpointId: endpointId)]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly()]);
        public Task<IReadOnlyList<AiAnomalyRecord>> SearchAsync(AiAnomalyRecordSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(empty ? [] : [Anomaly(customerId: request.CustomerId ?? "cust_123")]);
    }

    private sealed class FakeSecurityRepository(bool empty) : IAiSecurityAnalysisRepository
    {
        public Task InsertAsync(AiSecurityAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiSecurityAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiSecurityAnalysisResult?>(empty ? null : Security(eventId));
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>(empty ? [] : [Security(correlationId: correlationId)]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>(empty ? [] : [Security(customerId: customerId)]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>(empty ? [] : [Security()]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> SearchAsync(AiSecurityAnalysisSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>(empty ? [] : [Security(customerId: request.CustomerId ?? "cust_123")]);
    }

    private sealed class FakeRiskRepository(bool empty) : ICustomerEndpointRiskScoreRepository
    {
        public Task InsertAsync(CustomerEndpointRiskScoreResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>(empty ? [] : [Risk(customerId: customerId)]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>(empty ? [] : [Risk(subscriptionId: subscriptionId)]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>(empty ? [] : [Risk(endpointId: endpointId)]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>(empty ? [] : [Risk()]);
    }

    private sealed class FakeFailureAnomalyRepository(bool empty) : IWebhookFailureAnomalyDetectionRepository
    {
        public Task InsertAsync(WebhookFailureAnomalyDetectionResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(empty ? [] : [FailureAnomaly(customerId: customerId)]);
        public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(empty ? [] : [FailureAnomaly(subscriptionId: subscriptionId)]);
        public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(empty ? [] : [FailureAnomaly(endpointId: endpointId)]);
        public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(empty ? [] : [FailureAnomaly()]);
        public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetAnomaliesAsync(AiRiskLevel? minimumRiskLevel = null, int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(empty ? [] : [FailureAnomaly()]);
    }
}
