using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class SecurityAgentResultRepository : ISecurityAgentResultRepository
{
    private readonly IMongoCollection<SecurityAgentResult> _collection;
    public SecurityAgentResultRepository(ISecurityAgentResultCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(SecurityAgentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ReceivedAtUtc = DateTime.SpecifyKind(result.ReceivedAtUtc, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        result.SecurityRiskScore = Math.Clamp(result.SecurityRiskScore, 0, 100);
        result.ConfidenceScore = Math.Clamp(result.ConfidenceScore, 0, 1);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<SecurityAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<SecurityAgentResult>.Filter.Eq(result => result.EventId, eventId), Builders<SecurityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<SecurityAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<SecurityAgentResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<SecurityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<SecurityAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<SecurityAgentResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<SecurityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<SecurityAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<SecurityAgentResult>>(Array.Empty<SecurityAgentResult>()) : ToListAsync(Builders<SecurityAgentResult>.Filter.Empty, Builders<SecurityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<SecurityAgentResult>> SearchAsync(SecurityAgentSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<SecurityAgentResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.SecurityDecision is not null) filter &= builder.Eq(result => result.SecurityDecision, request.SecurityDecision.Value);
        if (request.RiskLevel is not null) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel.Value);
        if (request.IsSuspicious is not null) filter &= builder.Eq(result => result.IsSuspicious, request.IsSuspicious.Value);
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<SecurityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<SecurityAgentResult>> ToListAsync(FilterDefinition<SecurityAgentResult> filter, SortDefinition<SecurityAgentResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<SecurityAgentResult, SecurityAgentResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
