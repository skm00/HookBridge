using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.Persistence;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class MongoRepositoryTests
{
    [Fact]
    public async Task AddAsync_InsertsEntityIntoNamedCollection()
    {
        var fixture = new Fixture();
        var repository = fixture.CreateRepository();
        var subscription = new Subscription { Id = "sub-1", TenantId = "tenant-1" };

        await repository.AddAsync(subscription);

        fixture.Database.Verify(db => db.GetCollection<Subscription>(nameof(Subscription), null), Times.Once);
        fixture.Collection.Verify(
            collection => collection.InsertOneAsync(
                subscription,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesEntityById()
    {
        var fixture = new Fixture();
        var repository = fixture.CreateRepository();
        var subscription = new Subscription { Id = "sub-1", TenantId = "tenant-1", EventType = "order.created" };

        await repository.UpdateAsync(subscription);

        fixture.Collection.Verify(
            collection => collection.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                subscription,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesEntityById()
    {
        var fixture = new Fixture();
        var repository = fixture.CreateRepository();

        await repository.DeleteAsync("sub-1");

        fixture.Collection.Verify(
            collection => collection.DeleteOneAsync(
                It.IsAny<FilterDefinition<Subscription>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class Fixture
    {
        public Mock<IMongoDatabase> Database { get; } = new();
        public Mock<IMongoCollection<Subscription>> Collection { get; } = new();

        public MongoRepository<Subscription> CreateRepository()
        {
            Database
                .Setup(db => db.GetCollection<Subscription>(nameof(Subscription), null))
                .Returns(Collection.Object);

            return new MongoRepository<Subscription>(Database.Object);
        }
    }
}
