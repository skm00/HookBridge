using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAnomalyRecordRepository : IAiAnomalyRecordRepository
{
    private readonly IMongoCollection<AiAnomalyRecord> _collection;

    public AiAnomalyRecordRepository(IAiAnomalyRecordCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public async Task<AiAnomalyRecordRepositoryResult> InsertAsync(AiAnomalyRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var validationError = ValidateRecord(record);
        if (validationError is not null)
        {
            return AiAnomalyRecordRepositoryResult.Failure(record.AnomalyId, validationError);
        }

        record.CreatedAtUtc = DateTime.SpecifyKind(record.CreatedAtUtc, DateTimeKind.Utc);
        record.StoredAtUtc = DateTime.UtcNow;

        var existing = await GetByAnomalyIdAsync(record.AnomalyId, cancellationToken);
        if (existing is not null)
        {
            return AiAnomalyRecordRepositoryResult.Duplicate(record.AnomalyId, existing.Id);
        }

        try
        {
            await _collection.InsertOneAsync(record, cancellationToken: cancellationToken);
            return AiAnomalyRecordRepositoryResult.Success(record);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return AiAnomalyRecordRepositoryResult.Duplicate(record.AnomalyId);
        }
    }

    public Task<AiAnomalyRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.Id, id), cancellationToken);

    public Task<AiAnomalyRecord?> GetByAnomalyIdAsync(string anomalyId, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.AnomalyId, anomalyId), cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.EventId, eventId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.CorrelationId, correlationId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.CustomerId, customerId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.SubscriptionId, subscriptionId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAnomalyRecord>.Filter.Eq(record => record.EndpointId, endpointId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiAnomalyRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<AiAnomalyRecord>>(Array.Empty<AiAnomalyRecord>());
        }

        return ToListAsync(
            Builders<AiAnomalyRecord>.Filter.Empty,
            Builders<AiAnomalyRecord>.Sort.Descending(record => record.CreatedAtUtc),
            limit,
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AiAnomalyRecord>> SearchAsync(AiAnomalyRecordSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSearchRequest(request);

        var filter = BuildSearchFilter(request);
        var skip = (request.PageNumber - 1) * request.PageSize;

        return ToListAsync(
            filter,
            Builders<AiAnomalyRecord>.Sort.Descending(record => record.CreatedAtUtc),
            request.PageSize,
            skip,
            cancellationToken);
    }

    private static FilterDefinition<AiAnomalyRecord> BuildSearchFilter(AiAnomalyRecordSearchRequestDto request)
    {
        var builder = Builders<AiAnomalyRecord>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(record => record.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.CustomerIdType)) filter &= builder.Eq(record => record.CustomerIdType, request.CustomerIdType);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(record => record.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(record => record.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(record => record.Environment, request.Environment);
        if (!string.IsNullOrWhiteSpace(request.EventType)) filter &= builder.Eq(record => record.EventType, request.EventType);
        if (request.AnomalyType is not null) filter &= builder.Eq(record => record.AnomalyType, request.AnomalyType.Value.ToString());
        if (request.RiskLevel is not null) filter &= builder.Eq(record => record.RiskLevel, request.RiskLevel.Value.ToString());
        if (request.MinAnomalyScore is not null) filter &= builder.Gte(record => record.AnomalyScore, request.MinAnomalyScore.Value);
        if (request.MaxAnomalyScore is not null) filter &= builder.Lte(record => record.AnomalyScore, request.MaxAnomalyScore.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(record => record.CreatedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(record => record.CreatedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));

        return filter;
    }

    private static string? ValidateRecord(AiAnomalyRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.AnomalyId)) return "AnomalyId is required.";
        if (record.CreatedAtUtc.Kind != DateTimeKind.Utc) return "CreatedAtUtc must be UTC.";
        if (record.StoredAtUtc.Kind != DateTimeKind.Utc) return "StoredAtUtc must be UTC.";
        if (record.AnomalyScore is < 0 or > 100) return "AnomalyScore must be between 0 and 100.";
        if (!string.IsNullOrWhiteSpace(record.TargetUrl))
        {
            if (!Uri.TryCreate(record.TargetUrl, UriKind.Absolute, out var uri)) return "TargetUrl must be a valid absolute URL when provided.";
            if (uri.Scheme is not ("http" or "https")) return "TargetUrl must use HTTP or HTTPS when provided.";
        }

        return null;
    }

    private static void ValidateSearchRequest(AiAnomalyRecordSearchRequestDto request)
    {
        if (request.PageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(request), "PageNumber must be greater than 0.");
        if (request.PageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(request), "PageSize must be between 1 and 500.");
        if (request.MinAnomalyScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(request), "MinAnomalyScore must be between 0 and 100.");
        if (request.MaxAnomalyScore is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(request), "MaxAnomalyScore must be between 0 and 100.");
    }

    private async Task<AiAnomalyRecord?> FirstOrDefaultAsync(FilterDefinition<AiAnomalyRecord> filter, CancellationToken cancellationToken)
    {
        using var cursor = await _collection.FindAsync(filter, new FindOptions<AiAnomalyRecord> { Limit = 1 }, cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    private Task<IReadOnlyList<AiAnomalyRecord>> ToListAsync(
        FilterDefinition<AiAnomalyRecord> filter,
        SortDefinition<AiAnomalyRecord>? sort = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => ToListAsync(filter, sort, limit, null, cancellationToken);

    private async Task<IReadOnlyList<AiAnomalyRecord>> ToListAsync(
        FilterDefinition<AiAnomalyRecord> filter,
        SortDefinition<AiAnomalyRecord>? sort,
        int? limit,
        int? skip,
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<AiAnomalyRecord> { Sort = sort, Limit = limit, Skip = skip };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
