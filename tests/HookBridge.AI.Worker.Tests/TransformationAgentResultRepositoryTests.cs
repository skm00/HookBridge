using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class TransformationAgentResultRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<TransformationAgentResult>>();
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
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<TransformationAgentResult>>(), It.Is<FindOptions<TransformationAgentResult, TransformationAgentResult>>(options => options.Limit == 1 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("risk")]
    public async Task SearchAsync_FiltersByDecisionOrRiskLevel(string mode)
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);
        var request = mode == "decision"
            ? new TransformationAgentSearchRequestDto { TransformationDecision = TransformationAgentDecision.MappingReady }
            : new TransformationAgentSearchRequestDto { RiskLevel = "High" };

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<TransformationAgentResult>>(), It.IsAny<FindOptions<TransformationAgentResult, TransformationAgentResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateTransformationAgentResultIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateTransformationAgentResultIndexModels();
        indexes.Should().HaveCount(10);
        indexes.Select(index => index.Options.Name).Should().BeEquivalentTo(
            "idx_transformation_agent_results_event_id",
            "idx_transformation_agent_results_correlation_id",
            "idx_transformation_agent_results_customer_id",
            "idx_transformation_agent_results_subscription_id",
            "idx_transformation_agent_results_endpoint_id",
            "idx_transformation_agent_results_environment",
            "idx_transformation_agent_results_transformation_decision",
            "idx_transformation_agent_results_risk_level",
            "idx_transformation_agent_results_requires_approval",
            "idx_transformation_agent_results_generated_at_utc_desc");
    }

    private static TransformationAgentResultRepository CreateRepository(IMongoCollection<TransformationAgentResult> collection)
    {
        var provider = new Mock<ITransformationAgentResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new TransformationAgentResultRepository(provider.Object);
    }

    private static Mock<IMongoCollection<TransformationAgentResult>> CreateCollectionReturning(TransformationAgentResult result)
    {
        var cursor = new Mock<IAsyncCursor<TransformationAgentResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<TransformationAgentResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<TransformationAgentResult>>(), It.IsAny<FindOptions<TransformationAgentResult, TransformationAgentResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }

    private static TransformationAgentResult CreateResult() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        TransformationDecision = TransformationAgentDecision.MappingReady,
        RiskLevel = "High",
        ReceivedAtUtc = DateTime.UtcNow,
        GeneratedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };
}
