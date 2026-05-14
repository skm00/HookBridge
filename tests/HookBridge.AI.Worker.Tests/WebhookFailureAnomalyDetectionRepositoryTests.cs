using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailureAnomalyDetectionRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<WebhookFailureAnomalyDetectionResult>>();
        var repository = CreateRepository(collection.Object);
        var result = new WebhookFailureAnomalyDetectionResult
        {
            CustomerId = "cust_123",
            CalculatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        await repository.InsertAsync(result);

        result.CalculatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("subscription")]
    [InlineData("endpoint")]
    public async Task LookupMethods_CallMongoFindAsync(string lookup)
    {
        var collection = CreateCollectionReturning(new WebhookFailureAnomalyDetectionResult { CustomerId = "cust_123" }, out _);
        var repository = CreateRepository(collection.Object);

        var results = lookup switch
        {
            "customer" => await repository.GetByCustomerIdAsync("cust_123"),
            "subscription" => await repository.GetBySubscriptionIdAsync("sub_456"),
            _ => await repository.GetByEndpointIdAsync("endpoint_789")
        };

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<WebhookFailureAnomalyDetectionResult>>(),
            It.IsAny<FindOptions<WebhookFailureAnomalyDetectionResult, WebhookFailureAnomalyDetectionResult>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_CallsFindAsyncWithDescendingSortAndLimit()
    {
        var collection = CreateCollectionReturning(new WebhookFailureAnomalyDetectionResult { CustomerId = "cust_123" }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(25);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<WebhookFailureAnomalyDetectionResult>>(),
            It.Is<FindOptions<WebhookFailureAnomalyDetectionResult, WebhookFailureAnomalyDetectionResult>>(options => options.Limit == 25 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAnomaliesAsync_CallsFindAsyncWithSortAndLimit()
    {
        var collection = CreateCollectionReturning(new WebhookFailureAnomalyDetectionResult { CustomerId = "cust_123", IsAnomalyDetected = true, RiskLevel = AiRiskLevel.High.ToString() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetAnomaliesAsync(AiRiskLevel.High, 50);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<WebhookFailureAnomalyDetectionResult>>(),
            It.Is<FindOptions<WebhookFailureAnomalyDetectionResult, WebhookFailureAnomalyDetectionResult>>(options => options.Limit == 50 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateWebhookFailureAnomalyDetectionIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateWebhookFailureAnomalyDetectionIndexModels();
        indexes.Select(index => index.Options.Name).Should().BeEquivalentTo(
            "idx_webhook_failure_anomaly_customer_id",
            "idx_webhook_failure_anomaly_subscription_id",
            "idx_webhook_failure_anomaly_endpoint_id",
            "idx_webhook_failure_anomaly_event_type",
            "idx_webhook_failure_anomaly_environment",
            "idx_webhook_failure_anomaly_is_anomaly_detected",
            "idx_webhook_failure_anomaly_risk_level",
            "idx_webhook_failure_anomaly_calculated_at_utc_desc");
    }

    private static WebhookFailureAnomalyDetectionRepository CreateRepository(IMongoCollection<WebhookFailureAnomalyDetectionResult> collection)
    {
        var provider = new Mock<IWebhookFailureAnomalyDetectionCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new WebhookFailureAnomalyDetectionRepository(provider.Object);
    }

    private static Mock<IMongoCollection<WebhookFailureAnomalyDetectionResult>> CreateCollectionReturning(WebhookFailureAnomalyDetectionResult result, out Mock<IAsyncCursor<WebhookFailureAnomalyDetectionResult>> cursor)
    {
        cursor = new Mock<IAsyncCursor<WebhookFailureAnomalyDetectionResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<WebhookFailureAnomalyDetectionResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<WebhookFailureAnomalyDetectionResult>>(),
                It.IsAny<FindOptions<WebhookFailureAnomalyDetectionResult, WebhookFailureAnomalyDetectionResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }
}
