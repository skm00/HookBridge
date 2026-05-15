using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAgentOrchestrationRepository : IAiAgentOrchestrationRepository
{
    private readonly IMongoCollection<AiAgentOrchestrationResult> _collection;

    public AiAgentOrchestrationRepository(IAiAgentOrchestrationCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(AiAgentOrchestrationResult result, CancellationToken cancellationToken = default)
        => _collection.InsertOneAsync(result, cancellationToken: cancellationToken);

    public async Task<AiAgentOrchestrationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var cursor = await _collection.FindAsync(result => result.EventId == eventId, cancellationToken: cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAgentOrchestrationResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<AiAgentOrchestrationResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAgentOrchestrationResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<AiAgentOrchestrationResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiAgentOrchestrationResult>.Filter.Empty, Builders<AiAgentOrchestrationResult>.Sort.Descending(result => result.GeneratedAtUtc), Math.Max(1, limit), cancellationToken);

    public Task<IReadOnlyList<AiAgentOrchestrationResult>> SearchAsync(AiAgentOrchestrationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<AiAgentOrchestrationResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.RiskLevel is not null) filter &= builder.Eq(result => result.OverallRiskLevel, request.RiskLevel.Value.ToString());
        if (request.RecommendedAction is not null) filter &= builder.Eq(result => result.RecommendedAction, request.RecommendedAction.Value.ToString());
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));

        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<AiAgentOrchestrationResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<AiAgentOrchestrationResult>> ToListAsync(FilterDefinition<AiAgentOrchestrationResult> filter, SortDefinition<AiAgentOrchestrationResult>? sort, int? limit, CancellationToken cancellationToken)
    {
        var cursor = await _collection.FindAsync(filter, new FindOptions<AiAgentOrchestrationResult> { Sort = sort, Limit = limit }, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
