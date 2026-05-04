using HookBridge.Application.Interfaces;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class DataCleanupServiceTests
{
    [Fact]
    public async Task CleanupIncomingEventsAsync_DeletesOnlyDataOlderThanConfiguredRetention()
    {
        var utcNow = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
        var databaseMock = new Mock<IMongoDatabase>();
        var collectionMock = new Mock<IMongoCollection<IncomingEvent>>();
        FilterDefinition<IncomingEvent>? capturedFilter = null;

        collectionMock
            .Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<IncomingEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDeleteResult(10))
            .Callback<FilterDefinition<IncomingEvent>, CancellationToken>((filter, _) => capturedFilter = filter);

        databaseMock
            .Setup(db => db.GetCollection<IncomingEvent>(nameof(IncomingEvent), It.IsAny<MongoCollectionSettings?>()))
            .Returns(collectionMock.Object);

        var service = new DataCleanupService(databaseMock.Object, new FakeDateTimeProvider(utcNow), new TestLogger<DataCleanupService>());

        var deletedCount = await service.CleanupIncomingEventsAsync(30);

        Assert.Equal(10, deletedCount);
        Assert.NotNull(capturedFilter);
        Assert.Equal(utcNow.AddDays(-30), ExtractLessThanCutoff(capturedFilter!, nameof(IncomingEvent.ReceivedAt)));
    }

    [Fact]
    public async Task CleanupIncomingEventsAsync_DoesNotDeleteRecentDataWithinLast24Hours()
    {
        var utcNow = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
        var databaseMock = new Mock<IMongoDatabase>();
        var collectionMock = new Mock<IMongoCollection<IncomingEvent>>();
        FilterDefinition<IncomingEvent>? capturedFilter = null;

        collectionMock
            .Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<IncomingEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDeleteResult(0))
            .Callback<FilterDefinition<IncomingEvent>, CancellationToken>((filter, _) => capturedFilter = filter);

        databaseMock
            .Setup(db => db.GetCollection<IncomingEvent>(nameof(IncomingEvent), It.IsAny<MongoCollectionSettings?>()))
            .Returns(collectionMock.Object);

        var service = new DataCleanupService(databaseMock.Object, new FakeDateTimeProvider(utcNow), new TestLogger<DataCleanupService>());

        _ = await service.CleanupIncomingEventsAsync(1);

        Assert.NotNull(capturedFilter);
        Assert.Equal(utcNow.AddHours(-24), ExtractLessThanCutoff(capturedFilter!, nameof(IncomingEvent.ReceivedAt)));
    }

    [Fact]
    public async Task CleanupNotificationsAsync_UsesNotificationRetentionSettings()
    {
        var utcNow = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
        var databaseMock = new Mock<IMongoDatabase>();
        var collectionMock = new Mock<IMongoCollection<Notification>>();
        FilterDefinition<Notification>? capturedFilter = null;

        collectionMock
            .Setup(c => c.DeleteManyAsync(It.IsAny<FilterDefinition<Notification>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDeleteResult(4))
            .Callback<FilterDefinition<Notification>, CancellationToken>((filter, _) => capturedFilter = filter);

        databaseMock
            .Setup(db => db.GetCollection<Notification>(nameof(Notification), It.IsAny<MongoCollectionSettings?>()))
            .Returns(collectionMock.Object);

        var service = new DataCleanupService(databaseMock.Object, new FakeDateTimeProvider(utcNow), new TestLogger<DataCleanupService>());

        var deletedCount = await service.CleanupNotificationsAsync(14);

        Assert.Equal(4, deletedCount);
        Assert.NotNull(capturedFilter);
        Assert.Equal(utcNow.AddDays(-14), ExtractLessThanCutoff(capturedFilter!, nameof(Notification.CreatedAt)));
    }

    private static DateTime ExtractLessThanCutoff<TEntity>(FilterDefinition<TEntity> filter, string fieldName)
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<TEntity>();
        var renderedFilter = filter.Render(new RenderArgs<TEntity>(serializer, BsonSerializer.SerializerRegistry));
        var cutoffValue = renderedFilter[fieldName]["$lt"].ToUniversalTime();
        return DateTime.SpecifyKind(cutoffValue, DateTimeKind.Utc);
    }

    private static DeleteResult CreateDeleteResult(long deletedCount)
    {
        var result = new Mock<DeleteResult>();
        result.SetupGet(x => x.IsAcknowledged).Returns(true);
        result.SetupGet(x => x.DeletedCount).Returns(deletedCount);
        return result.Object;
    }

    private sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
