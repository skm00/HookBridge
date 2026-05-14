using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class CustomerEndpointRiskScoreRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<CustomerEndpointRiskScoreResult>>();
        var repository = CreateRepository(collection.Object);
        var result = new CustomerEndpointRiskScoreResult { CustomerId = "cust-1", CalculatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };

        await repository.InsertAsync(result);

        result.CalculatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("subscription")]
    [InlineData("endpoint")]
    public async Task QueryMethods_CallFindAsync(string queryType)
    {
        var collection = CreateCollectionReturning(new CustomerEndpointRiskScoreResult { CustomerId = "cust-1", SubscriptionId = "sub-1", EndpointId = "endpoint-1" });
        var repository = CreateRepository(collection.Object);

        var results = queryType switch
        {
            "customer" => await repository.GetByCustomerIdAsync("cust-1"),
            "subscription" => await repository.GetBySubscriptionIdAsync("sub-1"),
            _ => await repository.GetByEndpointIdAsync("endpoint-1")
        };

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<CustomerEndpointRiskScoreResult>>(), It.IsAny<FindOptions<CustomerEndpointRiskScoreResult, CustomerEndpointRiskScoreResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_CallsFindAsyncWithDescendingSortAndLimit()
    {
        var collection = CreateCollectionReturning(new CustomerEndpointRiskScoreResult { CustomerId = "cust-1" });
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(10);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<CustomerEndpointRiskScoreResult>>(),
            It.Is<FindOptions<CustomerEndpointRiskScoreResult, CustomerEndpointRiskScoreResult>>(options => options.Limit == 10 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateCustomerEndpointRiskScoreIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateCustomerEndpointRiskScoreIndexModels();
        indexes.Should().HaveCount(5);
        indexes.Select(index => index.Options.Name).Should().BeEquivalentTo(
            "idx_customer_endpoint_risk_score_customer_id",
            "idx_customer_endpoint_risk_score_subscription_id",
            "idx_customer_endpoint_risk_score_endpoint_id",
            "idx_customer_endpoint_risk_score_risk_level",
            "idx_customer_endpoint_risk_score_calculated_at_utc_desc");
    }

    private static CustomerEndpointRiskScoreRepository CreateRepository(IMongoCollection<CustomerEndpointRiskScoreResult> collection)
    {
        var provider = new Mock<ICustomerEndpointRiskScoreCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new CustomerEndpointRiskScoreRepository(provider.Object);
    }

    private static Mock<IMongoCollection<CustomerEndpointRiskScoreResult>> CreateCollectionReturning(CustomerEndpointRiskScoreResult result)
    {
        var cursor = new Mock<IAsyncCursor<CustomerEndpointRiskScoreResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<CustomerEndpointRiskScoreResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<CustomerEndpointRiskScoreResult>>(),
                It.IsAny<FindOptions<CustomerEndpointRiskScoreResult, CustomerEndpointRiskScoreResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }
}
