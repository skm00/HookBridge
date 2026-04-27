using System.Linq.Expressions;
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
    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var entities = await _collection.Find(predicate).ToListAsync(cancellationToken);
        return entities;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(
        Expression<Func<T, bool>> predicate,
        SortDefinition<T> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var totalCountTask = _collection.CountDocumentsAsync(predicate, cancellationToken: cancellationToken);
        var itemsTask = _collection
            .Find(predicate)
            .Sort(sort)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalCountTask, itemsTask);
        return (itemsTask.Result, totalCountTask.Result);
    }

    /// <inheritdoc />
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return _collection.Find(predicate).FirstOrDefaultAsync(cancellationToken);
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
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(current => current.Id, entity.Id);
        return _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(entity => entity.Id, id);
        return _collection.DeleteOneAsync(filter, cancellationToken);
    }
}
