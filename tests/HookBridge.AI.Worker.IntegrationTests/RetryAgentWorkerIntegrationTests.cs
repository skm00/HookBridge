using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.IntegrationTests;

[Collection(SampleWebhookFailureIntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Kafka")]
[Trait("Category", "Mongo")]
public sealed class RetryAgentWorkerIntegrationTests
{
    private readonly SampleWebhookFailureIntegrationTestFixture _fixture;

    public RetryAgentWorkerIntegrationTests(SampleWebhookFailureIntegrationTestFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task RetryAgentWorker_ConsumesEventAndStoresResult()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _fixture.ResetAsync(cts.Token);
        var eventId = $"retry_worker_{Guid.NewGuid():N}";

        await _fixture.PublishRetryAgentEventAsync(CreateRequest(eventId, statusCode: 429, riskLevel: "Medium"), cts.Token);

        var result = await _fixture.WaitForRetryAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.RetryDecision.Should().Be(RetryAgentDecision.RetryWithExponentialBackoff);
        result.RiskLevel.Should().Be("Medium");
    }

    [SkippableFact]
    public async Task RetryAgentWorker_HighRiskCreatesApprovalRecord()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _fixture.ResetAsync(cts.Token);
        var eventId = $"retry_high_{Guid.NewGuid():N}";

        await _fixture.PublishRetryAgentEventAsync(CreateRequest(eventId, statusCode: 500, riskLevel: "High"), cts.Token);

        var result = await _fixture.WaitForRetryAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.RequiresApproval.Should().BeTrue();
        var approval = await _fixture.GetCollection<AiRecommendationApproval>(AiMongoOptions.DefaultAiRecommendationApprovalsCollectionName)
            .Find(record => record.EventId == eventId)
            .FirstOrDefaultAsync(cts.Token);
        approval.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task RetryAgentWorker_PauseEndpointPublishesAnomalyEvent()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _fixture.ResetAsync(cts.Token);
        var eventId = $"retry_critical_{Guid.NewGuid():N}";

        await _fixture.PublishRetryAgentEventAsync(CreateRequest(eventId, statusCode: 500, riskLevel: "Critical"), cts.Token);

        var result = await _fixture.WaitForRetryAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.RetryDecision.Should().Be(RetryAgentDecision.PauseEndpoint);
        var anomaly = await _fixture.WaitForAnomalyRecordAsync(eventId, cts.Token);
        anomaly.Should().NotBeNull();
    }

    private static RetryAgentRequestDto CreateRequest(string eventId, int statusCode, string riskLevel) => new()
    {
        EventId = eventId,
        CorrelationId = $"corr_{eventId}",
        CustomerId = "cust-retry",
        CustomerIdType = "Tenant",
        SubscriptionId = "sub-retry",
        EndpointId = "endpoint-retry",
        Environment = "integration",
        EventType = "WebhookDeliveryFailed",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        StatusCode = statusCode,
        FailureReason = "failure",
        RetryCount = 1,
        MaxRetryCount = 5,
        EndpointRiskLevel = riskLevel,
        FailedAtUtc = DateTime.UtcNow
    };
}
