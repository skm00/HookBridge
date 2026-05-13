using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnalysisResultRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<AiAnalysisResult>>();
        var repository = CreateRepository(collection.Object);
        var result = new AiAnalysisResult
        {
            EventId = "evt-1",
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        await repository.InsertAsync(result);

        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(
            result,
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_CallsFindAsyncWithLimitOne()
    {
        var collection = CreateCollectionReturning(new AiAnalysisResult { Id = "507f1f77bcf86cd799439011" }, out var cursor);
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByIdAsync("507f1f77bcf86cd799439011");

        result.Should().NotBeNull();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.Is<FindOptions<AiAnalysisResult, AiAnalysisResult>>(options => options.Limit == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        cursor.Verify(mongoCursor => mongoCursor.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_CallsFindAsyncWithLimitOne()
    {
        var collection = CreateCollectionReturning(new AiAnalysisResult { EventId = "evt-1" }, out _);
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByEventIdAsync("evt-1");

        result.Should().NotBeNull();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.Is<FindOptions<AiAnalysisResult, AiAnalysisResult>>(options => options.Limit == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnsAllMatchingDocuments()
    {
        var collection = CreateCollectionReturning(
            new[]
            {
                new AiAnalysisResult { EventId = "evt-1", CorrelationId = "corr-1" },
                new AiAnalysisResult { EventId = "evt-2", CorrelationId = "corr-1" }
            },
            out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetByCorrelationIdAsync("corr-1");

        results.Should().HaveCount(2);
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.Is<FindOptions<AiAnalysisResult, AiAnalysisResult>>(options => options.Limit == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_CallsFindAsyncWithDescendingSortAndLimit()
    {
        var collection = CreateCollectionReturning(new AiAnalysisResult { EventId = "evt-1" }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(25);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.Is<FindOptions<AiAnalysisResult, AiAnalysisResult>>(options => options.Limit == 25 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_WithNonPositiveLimit_DoesNotCallMongo()
    {
        var collection = new Mock<IMongoCollection<AiAnalysisResult>>();
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(0);

        results.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.IsAny<FindOptions<AiAnalysisResult, AiAnalysisResult>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AiAnalysisResultRepository CreateRepository(IMongoCollection<AiAnalysisResult> collection)
    {
        var provider = new Mock<IAiAnalysisResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAnalysisResultRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiAnalysisResult>> CreateCollectionReturning(
        AiAnalysisResult result,
        out Mock<IAsyncCursor<AiAnalysisResult>> cursor)
    {
        return CreateCollectionReturning(new[] { result }, out cursor);
    }

    private static Mock<IMongoCollection<AiAnalysisResult>> CreateCollectionReturning(
        IReadOnlyCollection<AiAnalysisResult> results,
        out Mock<IAsyncCursor<AiAnalysisResult>> cursor)
    {
        cursor = new Mock<IAsyncCursor<AiAnalysisResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(results);

        var collection = new Mock<IMongoCollection<AiAnalysisResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<AiAnalysisResult>>(),
                It.IsAny<FindOptions<AiAnalysisResult, AiAnalysisResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);

        return collection;
    }
}
