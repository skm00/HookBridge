using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookDuplicateReplayDetectionServiceTests
{
    private static readonly DateTime ReceivedAt = new(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DetectAsync_DetectsSameEventIdDuplicate()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.ExistsByEventIdAsync("evt_1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var response = await CreateService(repository).DetectAsync(CreateRequest(eventId: "evt_1"));
        response.IsDuplicate.Should().BeTrue();
        response.DuplicateReason.Should().Be(WebhookDuplicateReplayReason.SameEventId);
        response.DetectionScore.Should().Be(50);
        response.RiskLevel.Should().Be(AiRiskLevel.Medium);
        response.SuggestedAction.Should().Be(WebhookDuplicateReplaySuggestedAction.IgnoreDuplicate);
    }

    [Fact]
    public async Task DetectAsync_DetectsSameCorrelationIdPossibleDuplicate()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.ExistsByCorrelationIdAsync("corr_1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var response = await CreateService(repository).DetectAsync(CreateRequest(correlationId: "corr_1"));
        response.IsDuplicate.Should().BeTrue();
        response.DuplicateReason.Should().Be(WebhookDuplicateReplayReason.SameCorrelationId);
        response.DetectionScore.Should().Be(25);
    }

    [Fact]
    public async Task DetectAsync_DetectsSamePayloadHashDuplicate()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.ExistsByPayloadHashAsync(It.IsAny<string>(), "cust_1", "sub_1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var response = await CreateService(repository).DetectAsync(CreateRequest());
        response.IsDuplicate.Should().BeTrue();
        response.DuplicateReason.Should().Be(WebhookDuplicateReplayReason.SamePayloadHash);
        response.DetectionScore.Should().Be(30);
    }

    [Fact]
    public async Task DetectAsync_DetectsSameSignatureHashReplay()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.ExistsBySignatureHashAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var response = await CreateService(repository).DetectAsync(CreateRequest(signature: "sha256=abc"));
        response.IsReplay.Should().BeTrue();
        response.ReplayReason.Should().Be(WebhookDuplicateReplayReason.SameSignatureHash);
        response.DetectionScore.Should().Be(40);
        response.SuggestedAction.Should().Be(WebhookDuplicateReplaySuggestedAction.Reject);
    }

    [Fact]
    public async Task DetectAsync_DetectsOldTimestampReplay()
    {
        var response = await CreateService().DetectAsync(CreateRequest(eventTimestampUtc: ReceivedAt.AddMinutes(-16)));
        response.IsReplay.Should().BeTrue();
        response.ReplayReason.Should().Be(WebhookDuplicateReplayReason.EventTimestampTooOld);
        response.DetectionScore.Should().Be(55);
        response.RiskLevel.Should().Be(AiRiskLevel.High);
    }

    [Fact]
    public async Task DetectAsync_DetectsFutureTimestampReplayRisk()
    {
        var response = await CreateService().DetectAsync(CreateRequest(eventTimestampUtc: ReceivedAt.AddMinutes(6)));
        response.IsReplay.Should().BeTrue();
        response.ReplayReason.Should().Be(WebhookDuplicateReplayReason.EventTimestampInFuture);
        response.DetectionScore.Should().Be(25);
    }

    [Fact]
    public async Task DetectAsync_DetectsHighFrequencyRepeat()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.SearchSimilarAsync("cust_1", "sub_1", "endpoint_1", It.IsAny<string>(), null, It.IsAny<DateTime>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 5).Select(_ => new WebhookEventFingerprint()).ToList());
        var response = await CreateService(repository).DetectAsync(CreateRequest());
        response.IsDuplicate.Should().BeTrue();
        response.IsReplay.Should().BeTrue();
        response.DuplicateReason.Should().Be(WebhookDuplicateReplayReason.HighFrequencyRepeat);
        response.DetectionScore.Should().Be(30);
    }


    [Fact]
    public async Task DetectAsync_WhenDisabled_AllowsWithoutRepositoryChecks()
    {
        var repository = CreateRepository();
        var response = await CreateService(repository, new DuplicateReplayDetectionOptions { Enabled = false }).DetectAsync(CreateRequest());

        response.IsDuplicate.Should().BeFalse();
        response.IsReplay.Should().BeFalse();
        response.DetectionScore.Should().Be(0);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.SuggestedAction.Should().Be(WebhookDuplicateReplaySuggestedAction.Allow);
        repository.Verify(x => x.ExistsByEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectAsync_WhenNoDuplicateOrReplayIndicators_AllowsEvent()
    {
        var response = await CreateService().DetectAsync(CreateRequest(includeEventTimestamp: false));

        response.IsDuplicate.Should().BeFalse();
        response.IsReplay.Should().BeFalse();
        response.DuplicateReason.Should().Be(WebhookDuplicateReplayReason.None);
        response.ReplayReason.Should().Be(WebhookDuplicateReplayReason.None);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.SuggestedAction.Should().Be(WebhookDuplicateReplaySuggestedAction.Allow);
    }

    [Fact]
    public async Task DetectAsync_RejectsMissingEventIdAndPayload()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().DetectAsync(CreateRequest(eventId: null, includePayload: false)));

    [Fact]
    public async Task DetectAsync_RejectsNonHttpTargetUrl()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().DetectAsync(CreateRequest(targetUrl: "ftp://example.com/webhook")));

    [Fact]
    public async Task DetectAsync_RejectsNonUtcEventTimestamp()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().DetectAsync(CreateRequest(eventTimestampUtc: DateTime.SpecifyKind(ReceivedAt, DateTimeKind.Local))));

    [Fact]
    public async Task DetectAsync_AllowsMissingOptionalIdentifiers()
    {
        var response = await CreateService().DetectAsync(CreateRequest(eventId: null, correlationId: null, targetUrl: null));

        response.EventId.Should().BeNull();
        response.CorrelationId.Should().BeNull();
        response.PayloadHash.Should().StartWith("sha256:");
        response.SuggestedAction.Should().Be(WebhookDuplicateReplaySuggestedAction.Allow);
    }

    [Fact]
    public void HashService_ProducesStableHashesForEquivalentJson()
    {
        var service = new WebhookFingerprintHashService();
        service.GeneratePayloadHash("{\"b\":2,\"a\":1}").Should().Be(service.GeneratePayloadHash("{\"a\":1,\"b\":2}"));
    }

    [Fact]
    public void HashService_ChangesHashForDifferentJson()
    {
        var service = new WebhookFingerprintHashService();
        service.GeneratePayloadHash("{\"a\":1}").Should().NotBe(service.GeneratePayloadHash("{\"a\":2}"));
    }


    [Fact]
    public void HashService_HashesRawPayloadWhenJsonParsingFails()
    {
        var service = new WebhookFingerprintHashService();

        service.GeneratePayloadHash("{not-json").Should().StartWith("sha256:");
    }

    [Fact]
    public void HashService_HandlesJsonElementAndObjectPayloads()
    {
        var service = new WebhookFingerprintHashService();
        using var document = System.Text.Json.JsonDocument.Parse("{\"b\":2,\"a\":1}");

        var elementHash = service.GeneratePayloadHash(document.RootElement);
        var objectHash = service.GeneratePayloadHash(new { b = 2, a = 1 });

        elementHash.Should().Be(service.GeneratePayloadHash("{\"a\":1,\"b\":2}"));
        objectHash.Should().StartWith("sha256:");
    }

    [Fact]
    public void HashService_ReturnsNullForMissingPayloadOrSignature()
    {
        var service = new WebhookFingerprintHashService();

        service.GeneratePayloadHash(null).Should().BeNull();
        service.GeneratePayloadHash(" ").Should().BeNull();
        service.GenerateSignatureHash(null).Should().BeNull();
        service.GenerateSignatureHash(" ").Should().BeNull();
    }

    [Fact]
    public void HashService_GeneratesSignatureHash()
    {
        new WebhookFingerprintHashService().GenerateSignatureHash("sha256=abc").Should().StartWith("sha256:");
    }

    [Fact]
    public async Task DetectAsync_ClampsDetectionScore()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.ExistsByEventIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.ExistsByCorrelationIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.ExistsByPayloadHashAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.ExistsBySignatureHashAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.SearchSimilarAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), null, It.IsAny<DateTime>(), 5, It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Range(0, 5).Select(_ => new WebhookEventFingerprint()).ToList());
        var response = await CreateService(repository).DetectAsync(CreateRequest(signature: "sig", eventTimestampUtc: ReceivedAt.AddMinutes(-20)));
        response.DetectionScore.Should().Be(100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
    }

    [Theory]
    [InlineData(0, AiRiskLevel.Low)]
    [InlineData(21, AiRiskLevel.Medium)]
    [InlineData(51, AiRiskLevel.High)]
    [InlineData(81, AiRiskLevel.Critical)]
    public void MapRiskLevel_UsesThresholds(int score, AiRiskLevel expected) => WebhookDuplicateReplayDetectionService.MapRiskLevel(score).Should().Be(expected);

    [Theory]
    [InlineData(AiRiskLevel.Low, WebhookDuplicateReplaySuggestedAction.Allow)]
    [InlineData(AiRiskLevel.Medium, WebhookDuplicateReplaySuggestedAction.Monitor)]
    [InlineData(AiRiskLevel.High, WebhookDuplicateReplaySuggestedAction.RequireManualReview)]
    [InlineData(AiRiskLevel.Critical, WebhookDuplicateReplaySuggestedAction.Quarantine)]
    public void MapSuggestedAction_UsesRisk(AiRiskLevel riskLevel, WebhookDuplicateReplaySuggestedAction expected)
        => WebhookDuplicateReplayDetectionService.MapSuggestedAction(riskLevel).Should().Be(expected);


    [Fact]
    public void MapRiskLevel_ReturnsUnknownWhenNoIdentifierOrPayloadHash()
        => WebhookDuplicateReplayDetectionService.MapRiskLevel(10, null, null).Should().Be(AiRiskLevel.Unknown);

    [Fact]
    public void MapSuggestedAction_ReturnsRejectForExpiredReplayReasons()
    {
        WebhookDuplicateReplayDetectionService.MapSuggestedAction(AiRiskLevel.High, replayReason: WebhookDuplicateReplayReason.EventTimestampTooOld)
            .Should().Be(WebhookDuplicateReplaySuggestedAction.Reject);
        WebhookDuplicateReplayDetectionService.MapSuggestedAction(AiRiskLevel.High, replayReason: WebhookDuplicateReplayReason.SignatureTimestampExpired)
            .Should().Be(WebhookDuplicateReplaySuggestedAction.Reject);
    }

    [Fact]
    public void MapSuggestedAction_ReturnsMonitorForUnknownRisk()
        => WebhookDuplicateReplayDetectionService.MapSuggestedAction(AiRiskLevel.Unknown).Should().Be(WebhookDuplicateReplaySuggestedAction.Monitor);

    [Fact]
    public void CreateFingerprint_CalculatesTtl()
    {
        var request = CreateRequest();
        var response = new WebhookDuplicateReplayDetectionResponseDto { PayloadHash = "sha256:x" };
        var created = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
        WebhookDuplicateReplayDetectionService.CreateFingerprint(request, response, new DuplicateReplayDetectionOptions { FingerprintTtlHours = 72 }, created).ExpiresAtUtc.Should().Be(created.AddHours(72));
    }

    [Fact]
    public async Task DetectAsync_RejectsInvalidUrl()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().DetectAsync(CreateRequest(targetUrl: "not-a-url")));

    [Fact]
    public async Task DetectAsync_RejectsNonUtcDates()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().DetectAsync(CreateRequest(receivedAtUtc: DateTime.SpecifyKind(ReceivedAt, DateTimeKind.Local))));

    private static WebhookDuplicateReplayDetectionService CreateService(Mock<IWebhookEventFingerprintRepository>? repository = null, DuplicateReplayDetectionOptions? options = null)
        => new(repository?.Object ?? CreateRepository().Object, new WebhookFingerprintHashService(), Options.Create(options ?? new DuplicateReplayDetectionOptions()));

    private static Mock<IWebhookEventFingerprintRepository> CreateRepository()
    {
        var repository = new Mock<IWebhookEventFingerprintRepository>();
        repository.Setup(x => x.GetRecentByCustomerAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WebhookEventFingerprint>());
        repository.Setup(x => x.SearchSimilarAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WebhookEventFingerprint>());
        return repository;
    }

    private static WebhookDuplicateReplayDetectionRequestDto CreateRequest(
        string? eventId = "evt_1",
        string? correlationId = "corr",
        string? signature = null,
        string? targetUrl = "https://example.com/webhook",
        DateTime? eventTimestampUtc = null,
        DateTime? receivedAtUtc = null,
        object? payload = null,
        bool includePayload = true,
        bool includeEventTimestamp = true)
        => new()
        {
            EventId = eventId,
            CorrelationId = correlationId,
            CustomerId = "cust_1",
            SubscriptionId = "sub_1",
            EndpointId = "endpoint_1",
            TargetUrl = targetUrl,
            Payload = includePayload ? payload ?? "{\"orderId\":\"ORD-1\",\"status\":\"Created\"}" : null,
            Signature = signature,
            EventTimestampUtc = includeEventTimestamp ? eventTimestampUtc ?? ReceivedAt : null,
            ReceivedAtUtc = receivedAtUtc ?? ReceivedAt
        };
}
