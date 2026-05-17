using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using MongoDB.Driver;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSafeModeAuditRepositoryTests
{
    [Fact]
    public async Task InsertAsync_SpecifiesUtcKindAndInsertsRecord()
    {
        var collection = new Mock<IMongoCollection<AiSafeModeAuditRecord>>();
        AiSafeModeAuditRecord? inserted = null;
        collection
            .Setup(c => c.InsertOneAsync(It.IsAny<AiSafeModeAuditRecord>(), It.IsAny<InsertOneOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<AiSafeModeAuditRecord, InsertOneOptions?, CancellationToken>((record, _, _) => inserted = record)
            .Returns(Task.CompletedTask);
        var repository = CreateRepository(collection.Object);
        var localTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);

        await repository.InsertAsync(new AiSafeModeAuditRecord
        {
            ActionType = AiActionType.RetryWebhook,
            Decision = AiSafeModeDecision.RequiresApproval,
            Environment = "production",
            EvaluatedAtUtc = localTime
        });

        inserted.Should().NotBeNull();
        inserted!.EvaluatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(c => c.InsertOneAsync(It.IsAny<AiSafeModeAuditRecord>(), It.IsAny<InsertOneOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsCursorResults()
    {
        var expected = new[] { Record("evt-1", "corr-1", AiActionType.RetryWebhook, AiSafeModeDecision.RequiresApproval) };
        var collection = CreateCollection(expected);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetByEventIdAsync("evt-1");

        results.Should().BeEquivalentTo(expected);
        collection.Verify(c => c.FindAsync(It.IsAny<FilterDefinition<AiSafeModeAuditRecord>>(), It.Is<FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>>(o => o.Limit == 100), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnsCursorResults()
    {
        var expected = new[] { Record("evt-1", "corr-1", AiActionType.MoveToDeadLetter, AiSafeModeDecision.Blocked) };
        var repository = CreateRepository(CreateCollection(expected).Object);

        var results = await repository.GetByCorrelationIdAsync("corr-1");

        results.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsEmptyWhenLimitIsInvalid()
    {
        var collection = CreateCollection(Array.Empty<AiSafeModeAuditRecord>());
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(0);

        results.Should().BeEmpty();
        collection.Verify(c => c.FindAsync(It.IsAny<FilterDefinition<AiSafeModeAuditRecord>>(), It.IsAny<FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_AppliesLimitAndReturnsResults()
    {
        var expected = new[] { Record("evt-2", "corr-2", AiActionType.QuarantineEvent, AiSafeModeDecision.RequiresApproval) };
        var collection = CreateCollection(expected);
        var repository = CreateRepository(collection.Object);

        var results = await repository.SearchAsync(new AiSafeModeAuditSearchRequestDto
        {
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "production",
            ActionType = AiActionType.QuarantineEvent,
            Decision = AiSafeModeDecision.RequiresApproval,
            FromUtc = DateTime.UtcNow.AddHours(-1),
            ToUtc = DateTime.UtcNow,
            Limit = 10
        });

        results.Should().BeEquivalentTo(expected);
        collection.Verify(c => c.FindAsync(It.IsAny<FilterDefinition<AiSafeModeAuditRecord>>(), It.Is<FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>>(o => o.Limit == 10), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ClampsInvalidLimitToDefault()
    {
        var collection = CreateCollection(Array.Empty<AiSafeModeAuditRecord>());
        var repository = CreateRepository(collection.Object);

        await repository.SearchAsync(new AiSafeModeAuditSearchRequestDto { Limit = -1 });

        collection.Verify(c => c.FindAsync(It.IsAny<FilterDefinition<AiSafeModeAuditRecord>>(), It.Is<FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>>(o => o.Limit == 100), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AiSafeModeAuditRecord Record(string eventId, string correlationId, AiActionType actionType, AiSafeModeDecision decision) => new()
    {
        EventId = eventId,
        CorrelationId = correlationId,
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "production",
        ActionType = actionType,
        Decision = decision,
        EvaluatedAtUtc = DateTime.UtcNow
    };

    private static AiSafeModeAuditRepository CreateRepository(IMongoCollection<AiSafeModeAuditRecord> collection)
    {
        var provider = new Mock<IAiSafeModeAuditRecordCollectionProvider>();
        provider.Setup(p => p.GetCollection()).Returns(collection);
        return new AiSafeModeAuditRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiSafeModeAuditRecord>> CreateCollection(IReadOnlyList<AiSafeModeAuditRecord> records)
    {
        var cursor = new Mock<IAsyncCursor<AiSafeModeAuditRecord>>();
        cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursor.SetupGet(c => c.Current).Returns(records);

        var collection = new Mock<IMongoCollection<AiSafeModeAuditRecord>>();
        collection
            .Setup(c => c.FindAsync(It.IsAny<FilterDefinition<AiSafeModeAuditRecord>>(), It.IsAny<FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }
}
