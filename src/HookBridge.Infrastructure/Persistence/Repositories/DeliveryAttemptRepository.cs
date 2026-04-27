using System.Text.RegularExpressions;
using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Repositories;

public sealed class DeliveryAttemptRepository(IMongoDatabase database) : IDeliveryAttemptRepository
{
    private readonly IMongoCollection<DeliveryAttempt> _collection = database.GetCollection<DeliveryAttempt>(nameof(DeliveryAttempt));

    public async Task<IReadOnlyList<DeliveryAttempt>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<DeliveryAttempt>>();

        if (!string.IsNullOrWhiteSpace(request.TenantId))
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.TenantId, request.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(request.EventId))
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.EventId, request.EventId));
        }

        if (!string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.SubscriptionId, request.SubscriptionId));
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.EventType, request.EventType));
        }

        if (request.Status.HasValue)
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.Status, request.Status.Value));
        }

        if (request.HttpStatusCode.HasValue)
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Eq(x => x.HttpStatusCode, request.HttpStatusCode.Value));
        }

        if (request.FromDate.HasValue)
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Gte(x => x.AttemptedAt, request.FromDate.Value));
        }

        if (request.ToDate.HasValue)
        {
            filters.Add(Builders<DeliveryAttempt>.Filter.Lte(x => x.AttemptedAt, request.ToDate.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            var escaped = Regex.Escape(request.TargetUrl);
            filters.Add(Builders<DeliveryAttempt>.Filter.Regex(
                x => x.TargetUrl,
                new BsonRegularExpression(escaped, "i")));
        }

        var filter = filters.Count == 0
            ? Builders<DeliveryAttempt>.Filter.Empty
            : Builders<DeliveryAttempt>.Filter.And(filters);

        return await _collection
            .Find(filter)
            .SortByDescending(x => x.AttemptedAt)
            .Limit(500)
            .ToListAsync(cancellationToken);
    }

    public Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }


    public Task<long> CountAsync(
        string tenantId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        DeliveryStatus? status,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DeliveryAttempt>.Filter.And(
            Builders<DeliveryAttempt>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<DeliveryAttempt>.Filter.Gte(x => x.AttemptedAt, fromDateInclusive),
            Builders<DeliveryAttempt>.Filter.Lt(x => x.AttemptedAt, toDateExclusive));

        if (status.HasValue)
        {
            filter &= Builders<DeliveryAttempt>.Filter.Eq(x => x.Status, status.Value);
        }

        return _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

}
