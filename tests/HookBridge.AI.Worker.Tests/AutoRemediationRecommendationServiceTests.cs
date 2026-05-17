using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.AutoRemediationRecommendation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AutoRemediationRecommendationServiceTests
{
    [Theory]
    [InlineData(429, AutoRemediationType.RetryTuning, AutoRemediationRecommendedAction.RetryWithBackoff, AutoRemediationReasonCode.RateLimited)]
    [InlineData(408, AutoRemediationType.TimeoutAdjustment, AutoRemediationRecommendedAction.RetryWithBackoff, AutoRemediationReasonCode.Timeout)]
    [InlineData(500, AutoRemediationType.RetryTuning, AutoRemediationRecommendedAction.RetryWithBackoff, AutoRemediationReasonCode.ServerError)]
    [InlineData(400, AutoRemediationType.PayloadContractReview, AutoRemediationRecommendedAction.ReviewPayloadContract, AutoRemediationReasonCode.ClientError)]
    [InlineData(401, AutoRemediationType.CredentialReview, AutoRemediationRecommendedAction.ReviewCredentials, AutoRemediationReasonCode.AuthenticationFailure)]
    public async Task RecommendAsync_MapsHttpStatusRules(int statusCode, AutoRemediationType type, AutoRemediationRecommendedAction action, AutoRemediationReasonCode reason)
    {
        var response = await CreateService().RecommendAsync(CreateRequest(statusCode: statusCode));

        Assert.Equal(type, response.RemediationType);
        Assert.Equal(action, response.RecommendedAction);
        Assert.Contains(reason, response.ReasonCodes);
        Assert.False(response.CanAutoApply);
        Assert.Equal(DateTimeKind.Utc, response.GeneratedAtUtc.Kind);
    }

    [Fact]
    public async Task RecommendAsync_RateLimitIncludesConcurrencyReductionStep()
    {
        var response = await CreateService().RecommendAsync(CreateRequest(statusCode: 429));

        Assert.Equal(AutoRemediationRecommendedAction.RetryWithBackoff, response.RecommendedAction);
        Assert.Contains(response.Steps, step => step.Contains("concurrency", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecommendAsync_MaxRetryReachedRequiresDeadLetterApproval()
    {
        var response = await CreateService().RecommendAsync(CreateRequest(retryCount: 5, maxRetryCount: 5));

        Assert.Equal(AutoRemediationType.DeadLetterReview, response.RemediationType);
        Assert.Equal(AutoRemediationRecommendedAction.MoveToDeadLetter, response.RecommendedAction);
        Assert.True(response.RequiresApproval);
    }

    [Fact]
    public async Task RecommendAsync_DeadLetterCountRecommendsReview()
    {
        var response = await CreateService().RecommendAsync(CreateRequest(deadLetterCount: 1));

        Assert.Equal(AutoRemediationRecommendedAction.ReviewDeadLetterQueue, response.RecommendedAction);
        Assert.True(response.RequiresApproval);
    }

    [Fact]
    public async Task RecommendAsync_OperationalHealthSignalsMapToInvestigations()
    {
        var service = CreateService();

        Assert.Equal(AutoRemediationRecommendedAction.CheckKafkaConsumers, (await service.RecommendAsync(CreateRequest(kafkaLag: 1001))).RecommendedAction);
        Assert.Equal(AutoRemediationRecommendedAction.CheckMongoHealth, (await service.RecommendAsync(CreateRequest(mongoHealthy: false))).RecommendedAction);
        Assert.Equal(AutoRemediationRecommendedAction.CheckMongoHealth, (await service.RecommendAsync(CreateRequest(mongoLatencyMs: 1001))).RecommendedAction);
    }

    [Fact]
    public async Task RecommendAsync_SecuritySignalsRequireApproval()
    {
        var suspicious = await CreateService().RecommendAsync(CreateRequest(isSuspicious: true));
        var replay = await CreateService().RecommendAsync(CreateRequest(isReplay: true));

        Assert.Equal(AutoRemediationRecommendedAction.QuarantineEvent, suspicious.RecommendedAction);
        Assert.True(suspicious.RequiresApproval);
        Assert.Equal(AutoRemediationRecommendedAction.QuarantineEvent, replay.RecommendedAction);
        Assert.True(replay.RequiresApproval);
    }

    [Fact]
    public async Task RecommendAsync_CriticalSignalsRequireApproval()
    {
        var endpoint = await CreateService().RecommendAsync(CreateRequest(endpointHealthStatus: "Critical"));
        var observability = await CreateService().RecommendAsync(CreateRequest(observabilityStatus: "Critical"));
        var highRisk = await CreateService().RecommendAsync(CreateRequest(riskLevel: "High", statusCode: 500));
        var criticalRisk = await CreateService().RecommendAsync(CreateRequest(riskLevel: "Critical", statusCode: 500));

        Assert.Equal(AutoRemediationRecommendedAction.PauseEndpoint, endpoint.RecommendedAction);
        Assert.True(endpoint.RequiresApproval);
        Assert.Equal(AutoRemediationRecommendedAction.EscalateToSupport, observability.RecommendedAction);
        Assert.True(highRisk.RequiresApproval);
        Assert.True(criticalRisk.RequiresApproval);
    }

    [Fact]
    public async Task RecommendAsync_LowConfidenceRequiresManualReview()
    {
        var response = await CreateService().RecommendAsync(CreateRequest(confidenceScore: 0.5, statusCode: 429));

        Assert.Equal(AutoRemediationType.ManualReview, response.RemediationType);
        Assert.Equal(AutoRemediationRecommendedAction.RequireManualReview, response.RecommendedAction);
        Assert.True(response.RequiresApproval);
        Assert.Equal(0.5, response.ConfidenceScore);
        Assert.Contains(AutoRemediationReasonCode.LowConfidence, response.ReasonCodes);
    }

    [Fact]
    public async Task RecommendAsync_ClampsResponseConfidence()
    {
        var response = await CreateService().RecommendAsync(CreateRequest(confidenceScore: 1.5, statusCode: 429));

        Assert.Equal(AutoRemediationType.RetryTuning, response.RemediationType);
        Assert.Equal(AutoRemediationRecommendedAction.RetryWithBackoff, response.RecommendedAction);
        Assert.False(response.RequiresApproval);
        Assert.Equal(1, response.ConfidenceScore);
    }

    private static AutoRemediationRecommendationService CreateService(AutoRemediationRecommendationOptions? options = null)
        => new(Options.Create(options ?? new AutoRemediationRecommendationOptions()), NullLogger<AutoRemediationRecommendationService>.Instance);

    private static AutoRemediationRecommendationRequestDto CreateRequest(
        int? statusCode = null,
        string riskLevel = "Medium",
        double confidenceScore = 0.82,
        int retryCount = 0,
        int maxRetryCount = 5,
        int deadLetterCount = 0,
        long kafkaLag = 0,
        bool? mongoHealthy = true,
        long mongoLatencyMs = 0,
        bool isSuspicious = false,
        bool isReplay = false,
        string? endpointHealthStatus = null,
        string? observabilityStatus = null) => new()
        {
            EventId = "evt_123",
            CorrelationId = "corr_123",
            CustomerId = "cust_123",
            RiskLevel = riskLevel,
            ConfidenceScore = confidenceScore,
            StatusCode = statusCode,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount,
            DeadLetterCount = deadLetterCount,
            KafkaConsumerLag = kafkaLag,
            MongoIsHealthy = mongoHealthy,
            MongoLatencyMs = mongoLatencyMs,
            IsSuspicious = isSuspicious,
            IsReplay = isReplay,
            EndpointHealthStatus = endpointHealthStatus,
            ObservabilityStatus = observabilityStatus,
            CreatedAtUtc = DateTime.UtcNow
        };
}
