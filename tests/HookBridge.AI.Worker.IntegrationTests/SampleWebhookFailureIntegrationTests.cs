using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Services.AiNaturalLanguageQuery;
using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.IntegrationTests;

[Collection(SampleWebhookFailureIntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Kafka")]
[Trait("Category", "Mongo")]
public sealed class SampleWebhookFailureIntegrationTests
{
    private readonly SampleWebhookFailureIntegrationTestFixture _fixture;

    public SampleWebhookFailureIntegrationTests(SampleWebhookFailureIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RateLimitFailure_ShouldGenerateRetryWithBackoffRecommendation()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_429");

        await _fixture.PublishAiAnalysisEventAsync("http-429-rate-limit-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff.ToString());
        result.RiskLevel.Should().BeOneOf(AiRiskLevel.Medium.ToString(), AiRiskLevel.High.ToString());
        result.IsRetryRecommended.Should().BeTrue();
    }

    [Fact]
    public async Task ServerErrorFailure_ShouldGenerateRetryWithBackoffRecommendation()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_500");

        await _fixture.PublishAiAnalysisEventAsync("http-500-server-error-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff.ToString());
        (result.AiSummary.Contains("server-side", StringComparison.OrdinalIgnoreCase) || result.AiSummary.Contains("receiver error", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task AuthFailure_ShouldRequireManualReview()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_401");

        await _fixture.PublishAiAnalysisEventAsync("http-401-auth-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RequireManualReview.ToString());
        result.RiskLevel.Should().Be(AiRiskLevel.High.ToString());
        (result.AiRecommendation.Contains("authentication", StringComparison.OrdinalIgnoreCase) || result.AiRecommendation.Contains("credentials", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task NotFoundFailure_ShouldRequireManualReviewOrDeadLetter()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_404");

        await _fixture.PublishAiAnalysisEventAsync("http-404-not-found-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().BeOneOf(SuggestedRetryAction.RequireManualReview.ToString(), SuggestedRetryAction.MoveToDeadLetter.ToString());
        (result.AiRecommendation.Contains("endpoint URL", StringComparison.OrdinalIgnoreCase) || result.AiRecommendation.Contains("missing resource", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task TimeoutFailure_ShouldRetryWithBackoff()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_timeout");

        await _fixture.PublishAiAnalysisEventAsync("timeout-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff.ToString());
        (result.AiRecommendation.Contains("timeout", StringComparison.OrdinalIgnoreCase) || result.AiRecommendation.Contains("receiver availability", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task MaxRetryReached_ShouldMoveToDeadLetter()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_max_retry");

        await _fixture.PublishAiAnalysisEventAsync("max-retry-reached-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.SuggestedRetryAction.Should().Be(SuggestedRetryAction.MoveToDeadLetter.ToString());
        result.IsRetryRecommended.Should().BeFalse();
        result.RiskLevel.Should().BeOneOf(AiRiskLevel.High.ToString(), AiRiskLevel.Critical.ToString());
    }

    [Fact]
    public async Task InvalidPayload_ShouldNotCrashWorker()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_invalid_payload");

        await _fixture.PublishAiAnalysisEventAsync("invalid-payload-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull("the worker should keep processing and persist a fallback/review result even when optional payload hints are malformed");
        result!.SuggestedRetryAction.Should().BeOneOf(SuggestedRetryAction.RequireManualReview.ToString(), SuggestedRetryAction.MoveToDeadLetter.ToString(), SuggestedRetryAction.RetryWithBackoff.ToString());
    }

    [Fact]
    public async Task SuspiciousPayload_ShouldCreateSecurityFindingAndAnomaly()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        _fixture.FakeLocalLlmClient.Mode = FakeLocalLlmMode.ProviderUnavailable;
        var eventId = UniqueEventId("evt_suspicious");

        await _fixture.PublishSecurityAnalysisEventAsync("suspicious-payload-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForSecurityResultAsync(eventId, cts.Token);
        var anomaly = await _fixture.WaitForAnomalyRecordAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.IsSuspicious.Should().BeTrue();
        result.RiskLevel.Should().BeOneOf(AiRiskLevel.High.ToString(), AiRiskLevel.Critical.ToString());
        anomaly.Should().NotBeNull();
    }

    [Fact]
    public async Task DuplicateEvent_ShouldDetectDuplicate()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_duplicate");

        await _fixture.PublishDuplicateReplayEventAsync("duplicate-event-failure.json", eventId, cts.Token);
        await WaitForFingerprintAsync(eventId, cts.Token);
        await _fixture.PublishDuplicateReplayEventAsync("duplicate-event-failure.json", eventId, cts.Token);
        var anomaly = await _fixture.WaitForAnomalyRecordAsync(eventId, cts.Token);

        anomaly.Should().NotBeNull();
        anomaly!.Summary.Contains("duplicate", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        (anomaly.Recommendation.Contains("Ignore", StringComparison.OrdinalIgnoreCase) || anomaly.Recommendation.Contains("manual review", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task ReplayEvent_ShouldDetectReplay()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_replay");

        await _fixture.PublishDuplicateReplayEventAsync("replay-event-failure.json", eventId, cts.Token, makeReplay: true);
        var anomaly = await _fixture.WaitForAnomalyRecordAsync(eventId, cts.Token);

        anomaly.Should().NotBeNull();
        anomaly!.Summary.Contains("replay", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        anomaly.RiskLevel.Should().BeOneOf(AiRiskLevel.High.ToString(), AiRiskLevel.Critical.ToString());
        (anomaly.Recommendation.Contains("Quarantine", StringComparison.OrdinalIgnoreCase) || anomaly.Recommendation.Contains("Reject", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task LlmUnavailable_ShouldUseFallbackAndStoreResult()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        _fixture.FakeLocalLlmClient.Mode = FakeLocalLlmMode.ProviderUnavailable;
        var eventId = UniqueEventId("evt_429_fallback");

        await _fixture.PublishAiAnalysisEventAsync("http-429-rate-limit-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.Fallback.Should().NotBeNull();
        result.Fallback!.UsedFallback.Should().BeTrue();
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff.ToString());
    }

    [Fact]
    public async Task InvalidLlmJson_ShouldUseFallbackAndStoreResult()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        _fixture.FakeLocalLlmClient.Mode = FakeLocalLlmMode.InvalidJson;
        var eventId = UniqueEventId("evt_500_invalid_json");

        await _fixture.PublishAiAnalysisEventAsync("http-500-server-error-failure.json", eventId, cts.Token);
        var result = await _fixture.WaitForMongoResultAsync(eventId, cts.Token);

        result.Should().NotBeNull();
        result!.Fallback.Should().NotBeNull();
        result.Fallback!.UsedFallback.Should().BeTrue();
        result.SuggestedRetryAction.Should().Be(SuggestedRetryAction.RetryWithBackoff.ToString());
    }

    [Fact]
    public async Task NaturalLanguageQuery_ShouldReturnFailureSummary()
    {
        if (_fixture.IsSkipped) return;
        using var cts = CreateTimeout();
        await _fixture.CleanMongoAsync(cts.Token);
        var eventId = UniqueEventId("evt_nlq");
        await _fixture.GetCollection<AiAnalysisResult>(AiMongoOptions.DefaultAiAnalysisResultsCollectionName).InsertOneAsync(new AiAnalysisResult
        {
            EventId = eventId,
            CorrelationId = $"corr_{eventId}",
            Source = "HookBridge.Worker",
            EventType = "WebhookDeliveryFailed",
            FailureReason = "Too Many Requests",
            AiSummary = "Webhook deliveries are failing because receivers are rate limiting and returning HTTP 429.",
            RootCause = "Receiver rate limiting",
            AiRecommendation = "Use exponential backoff and lower delivery concurrency.",
            RiskLevel = AiRiskLevel.Medium.ToString(),
            SuggestedRetryAction = SuggestedRetryAction.RetryWithBackoff.ToString(),
            IsRetryRecommended = true,
            Model = "fake-llm",
            Provider = "fake",
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken: cts.Token);

        var service = _fixture.GetRequiredService<IAiNaturalLanguageQueryService>();
        var response = await service.QueryAsync(new AiNaturalLanguageQueryRequestDto
        {
            Query = "Why are webhook deliveries failing?",
            FromUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-1), DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Utc),
            MaxResults = 5
        }, cts.Token);

        response.Answer.Should().NotBeNullOrWhiteSpace();
        response.Intent.Should().Be(AiNaturalLanguageQueryIntent.FailureAnalysis);
        response.Results.Should().NotBeEmpty();
        response.SuggestedActions.Should().NotBeEmpty();
    }

    private async Task WaitForFingerprintAsync(string eventId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = await _fixture.GetCollection<WebhookEventFingerprint>(AiMongoOptions.DefaultWebhookEventFingerprintsCollectionName)
                .Find(fingerprint => fingerprint.EventId == eventId)
                .AnyAsync(cancellationToken);
            if (exists)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException($"Fingerprint for event '{eventId}' was not persisted within the timeout.");
    }

    private static CancellationTokenSource CreateTimeout() => new(TimeSpan.FromSeconds(45));

    private static string UniqueEventId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
