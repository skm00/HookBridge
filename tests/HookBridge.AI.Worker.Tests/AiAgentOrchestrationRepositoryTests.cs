using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAgentOrchestrationRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsync()
    {
        var collection = new Mock<IMongoCollection<AiAgentOrchestrationResult>>();
        var repository = CreateRepository(collection.Object);
        var result = new AiAgentOrchestrationResult { EventId = "evt-1", GeneratedAtUtc = DateTime.UtcNow };

        await repository.InsertAsync(result);

        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(
            result,
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsMatchingDocument()
    {
        var collection = CreateCollectionReturning(new AiAgentOrchestrationResult { EventId = "evt-1" });
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByEventIdAsync("evt-1");

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt-1");
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
            It.IsAny<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }



    [Fact]
    public async Task QueryMethods_CallFindAsyncWithExpectedLimits()
    {
        var collection = CreateCollectionReturning(new AiAgentOrchestrationResult { EventId = "evt-1", CustomerId = "cust-1", CorrelationId = "corr-1" });
        var repository = CreateRepository(collection.Object);

        var byCorrelation = await repository.GetByCorrelationIdAsync("corr-1");
        var byCustomer = await repository.GetByCustomerIdAsync("cust-1");
        var recent = await repository.GetRecentAsync(5);

        byCorrelation.Should().ContainSingle();
        byCustomer.Should().ContainSingle();
        recent.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
            It.IsAny<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task SearchAsync_AppliesFiltersAndClampsLimit()
    {
        var collection = CreateCollectionReturning(new AiAgentOrchestrationResult { EventId = "evt-1", CustomerId = "cust-1" });
        var repository = CreateRepository(collection.Object);
        var request = new AiAgentOrchestrationSearchRequestDto
        {
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "end-1",
            Environment = "Production",
            RiskLevel = AiRiskLevel.High,
            RecommendedAction = AiOrchestrationRecommendedAction.RequireManualReview,
            RequiresApproval = true,
            FromUtc = DateTime.UtcNow.AddHours(-1),
            ToUtc = DateTime.UtcNow,
            Limit = 5_000
        };

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
            It.Is<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(options => options.Limit == 1000 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_UsesDefaultLimitForNonPositiveLimit()
    {
        var collection = CreateCollectionReturning(new AiAgentOrchestrationResult { EventId = "evt-1" });
        var repository = CreateRepository(collection.Object);

        var results = await repository.SearchAsync(new AiAgentOrchestrationSearchRequestDto { Limit = 0 });

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
            It.Is<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(options => options.Limit == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AiAgentOrchestrationRepository CreateRepository(IMongoCollection<AiAgentOrchestrationResult> collection)
    {
        var provider = new Mock<IAiAgentOrchestrationCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAgentOrchestrationRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiAgentOrchestrationResult>> CreateCollectionReturning(AiAgentOrchestrationResult result)
    {
        var collection = new Mock<IMongoCollection<AiAgentOrchestrationResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
                It.IsAny<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateCursor(result).Object);
        return collection;
    }

    private static Mock<IAsyncCursor<AiAgentOrchestrationResult>> CreateCursor(AiAgentOrchestrationResult result)
    {
        var cursor = new Mock<IAsyncCursor<AiAgentOrchestrationResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });
        return cursor;
    }
}
