using System.Linq.Expressions;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Interfaces.Persistence;

/// <summary>
/// Defines a generic repository contract for MongoDB-backed entities.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IMongoRepository<T>
    where T : BaseEntity
{
    /// <summary>
    /// Retrieves an entity by identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matched entity, or <c>null</c> when not found.</returns>
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching a predicate.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a filtered paged query and returns items and total count.
    /// </summary>
    Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(
        Expression<Func<T, bool>> predicate,
        SortDefinition<T> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the first entity matching a predicate or null if no match exists.
    /// </summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities from the collection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only list of entities.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
