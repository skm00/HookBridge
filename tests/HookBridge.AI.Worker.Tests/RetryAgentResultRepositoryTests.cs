using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class RetryAgentResultRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<RetryAgentResult>>();
        var repository = CreateRepository(collection.Object);
        var result = CreateResult();
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Unspecified);

        await repository.InsertAsync(result);

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsLatestResult()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByEventIdAsync("evt-1");

        result.Should().NotBeNull();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<RetryAgentResult>>(), It.Is<FindOptions<RetryAgentResult, RetryAgentResult>>(options => options.Limit == 1 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("risk")]
    public async Task SearchAsync_FiltersByDecisionOrRiskLevel(string mode)
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);
        var request = mode == "decision"
            ? new RetryAgentSearchRequestDto { RetryDecision = RetryAgentDecision.RetryWithExponentialBackoff }
            : new RetryAgentSearchRequestDto { RiskLevel = "High" };

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<RetryAgentResult>>(), It.IsAny<FindOptions<RetryAgentResult, RetryAgentResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateRetryAgentResultIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateRetryAgentResultIndexModels();
        indexes.Should().HaveCount(10);
        indexes.Select(index => index.Options.Name).Should().BeEquivalentTo(
            "idx_retry_agent_results_event_id",
            "idx_retry_agent_results_correlation_id",
            "idx_retry_agent_results_customer_id",
            "idx_retry_agent_results_subscription_id",
            "idx_retry_agent_results_endpoint_id",
            "idx_retry_agent_results_environment",
            "idx_retry_agent_results_retry_decision",
            "idx_retry_agent_results_risk_level",
            "idx_retry_agent_results_requires_approval",
            "idx_retry_agent_results_generated_at_utc_desc");
    }

    private static RetryAgentResultRepository CreateRepository(IMongoCollection<RetryAgentResult> collection)
    {
        var provider = new Mock<IRetryAgentResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new RetryAgentResultRepository(provider.Object);
    }

    private static Mock<IMongoCollection<RetryAgentResult>> CreateCollectionReturning(RetryAgentResult result)
    {
        var cursor = new Mock<IAsyncCursor<RetryAgentResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<RetryAgentResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<RetryAgentResult>>(), It.IsAny<FindOptions<RetryAgentResult, RetryAgentResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }

    private static RetryAgentResult CreateResult() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        RetryDecision = RetryAgentDecision.RetryWithExponentialBackoff,
        RiskLevel = "High",
        FailedAtUtc = DateTime.UtcNow,
        GeneratedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };
}
