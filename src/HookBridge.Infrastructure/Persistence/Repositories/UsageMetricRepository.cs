using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Repositories;

public sealed class UsageMetricRepository(IMongoDatabase database) : IUsageMetricRepository
{
    private readonly IMongoCollection<UsageMetric> _collection = database.GetCollection<UsageMetric>(nameof(UsageMetric));

    public Task<UsageMetric> GetOrCreateCurrentMonthAsync(
        string tenantId,
        int year,
        int month,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return UpsertAndReturnAsync(
            tenantId,
            year,
            month,
            nowUtc,
            Builders<UsageMetric>.Update.Set(x => x.LastUpdatedAt, nowUtc),
            cancellationToken);
    }

    public async Task IncrementEventsReceivedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await UpsertAndReturnAsync(
            tenantId,
            year,
            month,
            nowUtc,
            Builders<UsageMetric>.Update
                .Inc(x => x.EventsReceived, 1)
                .Set(x => x.LastUpdatedAt, nowUtc),
            cancellationToken);
    }

    public async Task IncrementEventsDeliveredAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await UpsertAndReturnAsync(
            tenantId,
            year,
            month,
            nowUtc,
            Builders<UsageMetric>.Update
                .Inc(x => x.EventsDelivered, 1)
                .Set(x => x.LastUpdatedAt, nowUtc),
            cancellationToken);
    }

    public async Task IncrementEventsFailedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await UpsertAndReturnAsync(
            tenantId,
            year,
            month,
            nowUtc,
            Builders<UsageMetric>.Update
                .Inc(x => x.EventsFailed, 1)
                .Set(x => x.LastUpdatedAt, nowUtc),
            cancellationToken);
    }

    private async Task<UsageMetric> UpsertAndReturnAsync(
        string tenantId,
        int year,
        int month,
        DateTime nowUtc,
        UpdateDefinition<UsageMetric> update,
        CancellationToken cancellationToken)
    {
        var filter = Builders<UsageMetric>.Filter.Where(x => x.TenantId == tenantId && x.Year == year && x.Month == month);
        var combined = Builders<UsageMetric>.Update.Combine(
            Builders<UsageMetric>.Update.SetOnInsert(x => x.Id, Guid.NewGuid().ToString("N")),
            Builders<UsageMetric>.Update.SetOnInsert(x => x.TenantId, tenantId),
            Builders<UsageMetric>.Update.SetOnInsert(x => x.Year, year),
            Builders<UsageMetric>.Update.SetOnInsert(x => x.Month, month),
            Builders<UsageMetric>.Update.SetOnInsert(x => x.CreatedAt, nowUtc),
            update);

        var options = new FindOneAndUpdateOptions<UsageMetric>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        return await _collection.FindOneAndUpdateAsync(filter, combined, options, cancellationToken);
    }
}
