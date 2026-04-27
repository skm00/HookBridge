using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(IMongoDatabase database) : INotificationRepository
{
    private readonly IMongoCollection<Notification> _collection = database.GetCollection<Notification>(nameof(Notification));

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        => _collection.InsertOneAsync(notification, cancellationToken: cancellationToken);

    public async Task<(IReadOnlyList<Notification> Items, long TotalCount)> SearchAsync(
        NotificationSearchRequestDto request,
        SortDefinition<Notification> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<Notification>>();

        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            filters.Add(Builders<Notification>.Filter.Eq(x => x.TenantId, request.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            filters.Add(Builders<Notification>.Filter.Eq(x => x.Type, request.Type));
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            filters.Add(Builders<Notification>.Filter.Eq(x => x.Severity, request.Severity));
        }

        if (request.IsRead.HasValue)
        {
            filters.Add(Builders<Notification>.Filter.Eq(x => x.IsRead, request.IsRead.Value));
        }

        if (request.FromDate.HasValue)
        {
            filters.Add(Builders<Notification>.Filter.Gte(x => x.CreatedAt, request.FromDate.Value));
        }

        if (request.ToDate.HasValue)
        {
            filters.Add(Builders<Notification>.Filter.Lte(x => x.CreatedAt, request.ToDate.Value));
        }

        var filter = filters.Count == 0
            ? Builders<Notification>.Filter.Empty
            : Builders<Notification>.Filter.And(filters);

        var countTask = _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var itemsTask = _collection.Find(filter).Sort(sort).Skip(skip).Limit(limit).ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);
        return (itemsTask.Result, countTask.Result);
    }

    public Task<Notification?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
        => _collection.ReplaceOneAsync(x => x.Id == notification.Id, notification, cancellationToken: cancellationToken);

    public async Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<Notification>.Filter.Eq(x => x.IsRead, false));

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task<bool> ExistsAsync(string tenantId, string type, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Notification>.Filter.And(
            Builders<Notification>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<Notification>.Filter.Eq(x => x.Type, type),
            Builders<Notification>.Filter.Gte(x => x.CreatedAt, fromInclusive),
            Builders<Notification>.Filter.Lt(x => x.CreatedAt, toExclusive));

        return await _collection.Find(filter).Limit(1).AnyAsync(cancellationToken);
    }
}
