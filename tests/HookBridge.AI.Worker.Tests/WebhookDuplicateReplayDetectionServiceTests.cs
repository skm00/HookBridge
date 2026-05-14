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
        response.DetectionScore.Should().Be(35);
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

    private static WebhookDuplicateReplayDetectionService CreateService(Mock<IWebhookEventFingerprintRepository>? repository = null)
        => new(repository?.Object ?? CreateRepository().Object, new WebhookFingerprintHashService(), Options.Create(new DuplicateReplayDetectionOptions()));

    private static Mock<IWebhookEventFingerprintRepository> CreateRepository()
    {
        var repository = new Mock<IWebhookEventFingerprintRepository>();
        repository.Setup(x => x.GetRecentByCustomerAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WebhookEventFingerprint>());
        repository.Setup(x => x.SearchSimilarAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WebhookEventFingerprint>());
        return repository;
    }

    private static WebhookDuplicateReplayDetectionRequestDto CreateRequest(string? eventId = "evt_1", string? correlationId = "corr", string? signature = null, string? targetUrl = "https://example.com/webhook", DateTime? eventTimestampUtc = null, DateTime? receivedAtUtc = null)
        => new()
        {
            EventId = eventId,
            CorrelationId = correlationId,
            CustomerId = "cust_1",
            SubscriptionId = "sub_1",
            EndpointId = "endpoint_1",
            TargetUrl = targetUrl,
            Payload = "{\"orderId\":\"ORD-1\",\"status\":\"Created\"}",
            Signature = signature,
            EventTimestampUtc = eventTimestampUtc ?? ReceivedAt,
            ReceivedAtUtc = receivedAtUtc ?? ReceivedAt
        };
}
