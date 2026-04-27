using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence;

/// <summary>
/// Generic MongoDB repository implementation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class MongoRepository<T>(IMongoDatabase database) : IMongoRepository<T>
    where T : BaseEntity
{
    private readonly IMongoCollection<T> _collection = database.GetCollection<T>(typeof(T).Name);

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(entity => entity.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Empty;
        var entities = await _collection.Find(filter).ToListAsync(cancellationToken);
        return entities;
    }

    /// <inheritdoc />
    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<T>.Filter.Eq(current => current.Id, entity.Id);
        await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(entity => entity.Id, id);
        return _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
