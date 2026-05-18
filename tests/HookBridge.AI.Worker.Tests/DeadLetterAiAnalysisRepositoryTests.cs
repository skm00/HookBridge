using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class DeadLetterAiAnalysisRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<DeadLetterAiAnalysisResult>>();
        var repository = CreateRepository(collection.Object);
        var result = CreateResult();
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Unspecified);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Unspecified);

        await repository.InsertAsync(result);

        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByDeadLetterIdAsync_ReturnsLatestResult()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByDeadLetterIdAsync("dlq-1");

        result.Should().NotBeNull();
        result!.DeadLetterId.Should().Be("dlq-1");
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Limit == 1 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsLatestResult()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByEventIdAsync("evt-1");

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt-1");
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Limit == 1 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("correlation")]
    [InlineData("customer")]
    public async Task LookupListMethods_ReturnResults(string mode)
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var results = mode == "correlation"
            ? await repository.GetByCorrelationIdAsync("corr-1")
            : await repository.GetByCustomerIdAsync("cust-1");

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsEmpty_WhenLimitIsZero()
    {
        var collection = new Mock<IMongoCollection<DeadLetterAiAnalysisResult>>();
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(0);

        results.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.IsAny<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRecentAsync_UsesRequestedLimit()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(25);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Limit == 25 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_AppliesAllSupportedFiltersAndCapsLimit()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);
        var request = new DeadLetterAiAnalysisSearchRequestDto
        {
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "qa",
            ReplaySafety = DeadLetterReplaySafety.ReplayWithCaution,
            SuggestedAction = DeadLetterSuggestedAction.ReplayWithBackoff,
            RiskLevel = "Medium",
            RequiresApproval = true,
            FromUtc = DateTime.UtcNow.AddHours(-1),
            ToUtc = DateTime.UtcNow,
            Limit = 5000
        };

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Limit == 1000 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_UsesDefaultLimit_WhenLimitIsNotPositive()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var results = await repository.SearchAsync(new DeadLetterAiAnalysisSearchRequestDto { Limit = 0 });

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.Is<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(options => options.Limit == 100), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DeadLetterAiAnalysisRepository CreateRepository(IMongoCollection<DeadLetterAiAnalysisResult> collection)
    {
        var provider = new Mock<IDeadLetterAiAnalysisCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new DeadLetterAiAnalysisRepository(provider.Object);
    }

    private static Mock<IMongoCollection<DeadLetterAiAnalysisResult>> CreateCollectionReturning(DeadLetterAiAnalysisResult result)
    {
        var cursor = new Mock<IAsyncCursor<DeadLetterAiAnalysisResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<DeadLetterAiAnalysisResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<DeadLetterAiAnalysisResult>>(), It.IsAny<FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }

    private static DeadLetterAiAnalysisResult CreateResult() => new()
    {
        DeadLetterId = "dlq-1",
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        ReplaySafety = DeadLetterReplaySafety.ReplayWithCaution,
        SuggestedAction = DeadLetterSuggestedAction.ReplayWithBackoff,
        RiskLevel = "Medium",
        RequiresApproval = true,
        Summary = "summary",
        Recommendation = "recommendation",
        CreatedAtUtc = DateTime.UtcNow,
        GeneratedAtUtc = DateTime.UtcNow
    };
}
