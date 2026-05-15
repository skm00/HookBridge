using FluentAssertions;
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

    private static AiAgentOrchestrationRepository CreateRepository(IMongoCollection<AiAgentOrchestrationResult> collection)
    {
        var provider = new Mock<IAiAgentOrchestrationCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAgentOrchestrationRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiAgentOrchestrationResult>> CreateCollectionReturning(AiAgentOrchestrationResult result)
    {
        var cursor = new Mock<IAsyncCursor<AiAgentOrchestrationResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<AiAgentOrchestrationResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<AiAgentOrchestrationResult>>(),
                It.IsAny<FindOptions<AiAgentOrchestrationResult, AiAgentOrchestrationResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }
}
