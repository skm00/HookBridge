using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookEventFingerprintRepositoryTests
{
    [Fact]
    public void CreateWebhookEventFingerprintIndexModels_IncludesRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateWebhookEventFingerprintIndexModels();
        indexes.Should().HaveCount(12);
        indexes.Select(x => x.Options.Name).Should().Contain(new[]
        {
            "idx_webhook_event_fingerprints_event_id",
            "idx_webhook_event_fingerprints_correlation_id",
            "idx_webhook_event_fingerprints_customer_id",
            "idx_webhook_event_fingerprints_subscription_id",
            "idx_webhook_event_fingerprints_endpoint_id",
            "idx_webhook_event_fingerprints_payload_hash",
            "idx_webhook_event_fingerprints_signature_hash",
            "idx_webhook_event_fingerprints_received_at_desc",
            "idx_webhook_event_fingerprints_expires_at_ttl",
            "idx_webhook_event_fingerprints_customer_payload_received_desc",
            "idx_webhook_event_fingerprints_subscription_event",
            "idx_webhook_event_fingerprints_endpoint_signature"
        });
        indexes.Single(x => x.Options.Name == "idx_webhook_event_fingerprints_expires_at_ttl").Options.ExpireAfter.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AiKafkaTopics_IncludesDuplicateReplayDetectionTopic()
        => AiKafkaTopics.DuplicateReplayDetection.Should().Be("hookbridge.ai.duplicate-replay-detection");

    [Fact]
    public void RequiredServices_AreRegisteredInDi()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DuplicateReplayDetection:Enabled"] = "true"
        }).Build();
        services.AddDuplicateReplayDetectionOptions(config);
        services.AddWebhookDuplicateReplayDetectionServices();
        services.AddSingleton<IWebhookEventFingerprintRepository, FakeFingerprintRepository>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWebhookFingerprintHashService>().Should().BeOfType<WebhookFingerprintHashService>();
        provider.GetRequiredService<IWebhookDuplicateReplayDetectionService>().Should().BeOfType<WebhookDuplicateReplayDetectionService>();
        provider.GetRequiredService<IWebhookEventFingerprintRepository>().Should().BeOfType<FakeFingerprintRepository>();
    }

    [Fact]
    public void AiMongoOptions_DefaultsToWebhookEventFingerprintsCollection()
        => new AiMongoOptions().WebhookEventFingerprintsCollectionName.Should().Be("webhook_event_fingerprints");

    [Fact]
    public async Task ExistsByEventIdAsync_UsesMongoCollectionCount()
    {
        var collection = new Mock<IMongoCollection<WebhookEventFingerprint>>();
        collection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<WebhookEventFingerprint>>(), It.IsAny<CountOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var repository = new WebhookEventFingerprintRepository(collection.Object);

        var exists = await repository.ExistsByEventIdAsync("evt_1");

        exists.Should().BeTrue();
        collection.Verify(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<WebhookEventFingerprint>>(), It.Is<CountOptions>(o => o.Limit == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FakeFingerprintRepository : IWebhookEventFingerprintRepository
    {
        public Task InsertAsync(WebhookEventFingerprint fingerprint, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsByPayloadHashAsync(string payloadHash, string? customerId = null, string? subscriptionId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsBySignatureHashAsync(string signatureHash, DateTime? receivedAfterUtc = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<WebhookEventFingerprint>> GetRecentByCustomerAsync(string customerId, DateTime receivedAfterUtc, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookEventFingerprint>>(Array.Empty<WebhookEventFingerprint>());
        public Task<IReadOnlyList<WebhookEventFingerprint>> SearchSimilarAsync(string? customerId, string? subscriptionId, string? endpointId, string? payloadHash, string? signatureHash, DateTime? receivedAfterUtc, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WebhookEventFingerprint>>(Array.Empty<WebhookEventFingerprint>());
    }
}
