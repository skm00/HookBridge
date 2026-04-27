using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Repositories;

public sealed class FailedEventRepository(IMongoDatabase database) : IFailedEventRepository
{
    private readonly IMongoCollection<FailedEvent> _collection = database.GetCollection<FailedEvent>(nameof(FailedEvent));

    public Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
    {
        return _collection.InsertOneAsync(failedEvent, cancellationToken: cancellationToken);
    }

    public async Task<(IReadOnlyList<FailedEvent> Items, long TotalCount)> SearchAsync(
        FailedEventSearchRequestDto request,
        SortDefinition<FailedEvent> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<FailedEvent>>();

        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            filters.Add(Builders<FailedEvent>.Filter.Eq(x => x.TenantId, request.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(request.EventId))
        {
            filters.Add(Builders<FailedEvent>.Filter.Eq(x => x.EventId, request.EventId));
        }

        if (!string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            filters.Add(Builders<FailedEvent>.Filter.Eq(x => x.SubscriptionId, request.SubscriptionId));
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            filters.Add(Builders<FailedEvent>.Filter.Eq(x => x.EventType, request.EventType));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            filters.Add(Builders<FailedEvent>.Filter.Eq(x => x.Status, request.Status));
        }

        if (request.FromDate.HasValue)
        {
            filters.Add(Builders<FailedEvent>.Filter.Gte(x => x.FailedAt, request.FromDate.Value));
        }

        if (request.ToDate.HasValue)
        {
            filters.Add(Builders<FailedEvent>.Filter.Lte(x => x.FailedAt, request.ToDate.Value));
        }

        var filter = filters.Count == 0
            ? Builders<FailedEvent>.Filter.Empty
            : Builders<FailedEvent>.Filter.And(filters);

        var countTask = _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = _collection
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return (itemsTask.Result, countTask.Result);
    }

    public Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public Task UpdateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
    {
        return _collection.ReplaceOneAsync(
            x => x.Id == failedEvent.Id,
            failedEvent,
            cancellationToken: cancellationToken);
    }


    public Task<long> CountByStatusAsync(
        string tenantId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<FailedEvent>.Filter.And(
            Builders<FailedEvent>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<FailedEvent>.Filter.Eq(x => x.Status, status));

        return _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

}
