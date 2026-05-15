using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using Xunit;

namespace HookBridge.AI.Worker.IntegrationTests;

[Collection(SampleWebhookFailureIntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Kafka")]
[Trait("Category", "Mongo")]
public sealed class SecurityAgentWorkerIntegrationTests
{
    private readonly SampleWebhookFailureIntegrationTestFixture _fixture;
    public SecurityAgentWorkerIntegrationTests(SampleWebhookFailureIntegrationTestFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task SecurityAgentWorker_ConsumesEventAndStoresResult()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        await _fixture.CleanMongoAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var eventId = $"security_{Guid.NewGuid():N}";

        await _fixture.PublishSecurityAgentEventAsync(CreateRequest(eventId, signatureFailed: true), cts.Token);

        var result = await _fixture.WaitForSecurityAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.SecurityDecision.Should().NotBe(SecurityAgentDecision.Allow);
        result.RequiresApproval.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SecurityAgentWorker_HighRiskPublishesAnomalyAndCreatesApproval()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        await _fixture.CleanMongoAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var eventId = $"security_high_{Guid.NewGuid():N}";

        await _fixture.PublishSecurityAgentEventAsync(CreateRequest(eventId, signatureFailed: true, authFailed: true), cts.Token);

        var result = await _fixture.WaitForSecurityAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.RiskLevel.Should().Be(AiRiskLevel.High);
        (await _fixture.WaitForAnomalyRecordAsync(eventId, cts.Token)).Should().NotBeNull();
        (await _fixture.WaitForApprovalAsync(eventId, cts.Token)).Should().NotBeNull();
    }

    [SkippableFact]
    public async Task SecurityAgentWorker_ReplayEventReturnsQuarantineOrReject()
    {
        Skip.If(_fixture.IsSkipped, _fixture.SkipReason);
        await _fixture.CleanMongoAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var eventId = $"security_replay_{Guid.NewGuid():N}";

        await _fixture.PublishSecurityAgentEventAsync(CreateRequest(eventId, isReplay: true), cts.Token);

        var result = await _fixture.WaitForSecurityAgentResultAsync(eventId, cts.Token);
        result.Should().NotBeNull();
        result!.SecurityDecision.Should().BeOneOf(SecurityAgentDecision.Quarantine, SecurityAgentDecision.Reject);
    }

    private static SecurityAgentRequestDto CreateRequest(string eventId, bool signatureFailed = false, bool authFailed = false, bool isReplay = false) => new()
    {
        EventId = eventId,
        CorrelationId = $"corr_{eventId}",
        CustomerId = "cust-security",
        SubscriptionId = "sub-security",
        EndpointId = "endpoint-security",
        Environment = "integration",
        EventType = "OrderCreated",
        Source = "HookBridge.Tests",
        TargetUrl = "https://example.test/webhook",
        HttpMethod = "POST",
        UserAgent = "HookBridgeIntegrationTests",
        SignatureValidationFailed = signatureFailed,
        AuthenticationFailed = authFailed,
        IsReplay = isReplay,
        PayloadSizeBytes = 256,
        Payload = new { id = eventId, comment = signatureFailed ? "<script>alert('x')</script>" : "ok" },
        ReceivedAtUtc = DateTime.UtcNow
    };
}
