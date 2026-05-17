using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiDecisionAuditRepository : IAiDecisionAuditRepository
{
    private readonly IMongoCollection<AiDecisionAuditRecord> _collection;

    public AiDecisionAuditRepository(IAiDecisionAuditRecordCollectionProvider collectionProvider)
        => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(AiDecisionAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.AuditId)) throw new ArgumentException("AuditId is required.", nameof(record));
        if (record.DecisionType == AiDecisionAuditType.Unknown) throw new ArgumentException("DecisionType is required.", nameof(record));
        record.CreatedAtUtc = EnsureUtc(record.CreatedAtUtc, nameof(record.CreatedAtUtc));
        if (record.ConfidenceScore is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(record), "ConfidenceScore must be between 0 and 1.");
        return _collection.InsertOneAsync(record, cancellationToken: cancellationToken);
    }

    public async Task<AiDecisionAuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _)) return null;
        var results = await ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Eq(record => record.Id, id), null, 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<AiDecisionAuditRecord?> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditId)) return null;
        var results = await ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Eq(record => record.AuditId, auditId), null, 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(eventId) ? Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Array.Empty<AiDecisionAuditRecord>()) : ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Eq(record => record.EventId, eventId), SortByCreatedAtDesc(), null, cancellationToken);

    public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(correlationId) ? Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Array.Empty<AiDecisionAuditRecord>()) : ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Eq(record => record.CorrelationId, correlationId), SortByCreatedAtDesc(), null, cancellationToken);

    public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(customerId) ? Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Array.Empty<AiDecisionAuditRecord>()) : ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Eq(record => record.CustomerId, customerId), SortByCreatedAtDesc(), null, cancellationToken);

    public Task<IReadOnlyList<AiDecisionAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Array.Empty<AiDecisionAuditRecord>()) : ToListAsync(Builders<AiDecisionAuditRecord>.Filter.Empty, SortByCreatedAtDesc(), Math.Min(limit, 500), cancellationToken);

    public Task<IReadOnlyList<AiDecisionAuditRecord>> SearchAsync(AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSearch(request);
        var builder = Builders<AiDecisionAuditRecord>.Filter;
        var filter = builder.Empty;
        AddEqIfPresent(ref filter, builder, record => record.EventId, request.EventId);
        AddEqIfPresent(ref filter, builder, record => record.CorrelationId, request.CorrelationId);
        AddEqIfPresent(ref filter, builder, record => record.CustomerId, request.CustomerId);
        AddEqIfPresent(ref filter, builder, record => record.CustomerIdType, request.CustomerIdType);
        AddEqIfPresent(ref filter, builder, record => record.SubscriptionId, request.SubscriptionId);
        AddEqIfPresent(ref filter, builder, record => record.EndpointId, request.EndpointId);
        AddEqIfPresent(ref filter, builder, record => record.Environment, request.Environment);
        AddEqIfPresent(ref filter, builder, record => record.AgentName, request.AgentName);
        if (request.DecisionType is not null) filter &= builder.Eq(record => record.DecisionType, request.DecisionType.Value);
        AddEqIfPresent(ref filter, builder, record => record.RiskLevel, request.RiskLevel);
        AddEqIfPresent(ref filter, builder, record => record.ConfidenceLevel, request.ConfidenceLevel);
        AddEqIfPresent(ref filter, builder, record => record.SuggestedAction, request.SuggestedAction);
        if (request.RequiresApproval is not null) filter &= builder.Eq(record => record.RequiresApproval, request.RequiresApproval.Value);
        AddEqIfPresent(ref filter, builder, record => record.ApprovalStatus, request.ApprovalStatus);
        AddEqIfPresent(ref filter, builder, record => record.SafeModeDecision, request.SafeModeDecision);
        if (request.IsActionAllowed is not null) filter &= builder.Eq(record => record.IsActionAllowed, request.IsActionAllowed.Value);
        if (request.UsedFallback is not null) filter &= builder.Eq(record => record.UsedFallback, request.UsedFallback.Value);
        AddEqIfPresent(ref filter, builder, record => record.FallbackReason, request.FallbackReason);
        AddEqIfPresent(ref filter, builder, record => record.PromptName, request.PromptName);
        AddEqIfPresent(ref filter, builder, record => record.PromptVersion, request.PromptVersion);
        AddEqIfPresent(ref filter, builder, record => record.Model, request.Model);
        AddEqIfPresent(ref filter, builder, record => record.Provider, request.Provider);
        if (request.FromUtc is not null) filter &= builder.Gte(record => record.CreatedAtUtc, request.FromUtc.Value);
        if (request.ToUtc is not null) filter &= builder.Lte(record => record.CreatedAtUtc, request.ToUtc.Value);
        var skip = (request.PageNumber - 1) * request.PageSize;
        return ToListAsync(filter, SortByCreatedAtDesc(), request.PageSize, cancellationToken, skip);
    }

    private async Task<IReadOnlyList<AiDecisionAuditRecord>> ToListAsync(FilterDefinition<AiDecisionAuditRecord> filter, SortDefinition<AiDecisionAuditRecord>? sort, int? limit, CancellationToken cancellationToken, int? skip = null)
    {
        var options = new FindOptions<AiDecisionAuditRecord, AiDecisionAuditRecord> { Sort = sort, Limit = limit, Skip = skip };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    private static SortDefinition<AiDecisionAuditRecord> SortByCreatedAtDesc() => Builders<AiDecisionAuditRecord>.Sort.Descending(record => record.CreatedAtUtc);

    private static void AddEqIfPresent(ref FilterDefinition<AiDecisionAuditRecord> filter, FilterDefinitionBuilder<AiDecisionAuditRecord> builder, System.Linq.Expressions.Expression<Func<AiDecisionAuditRecord, string?>> field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) filter &= builder.Eq(field, value);
    }

    public static void ValidateSearch(AiDecisionAuditSearchRequestDto request)
    {
        if (request.PageNumber <= 0) throw new ArgumentException("PageNumber must be greater than 0.", nameof(request));
        if (request.PageSize is < 1 or > 500) throw new ArgumentException("PageSize must be between 1 and 500.", nameof(request));
        if (request.FromUtc is not null) EnsureUtc(request.FromUtc.Value, nameof(request.FromUtc));
        if (request.ToUtc is not null) EnsureUtc(request.ToUtc.Value, nameof(request.ToUtc));
        if (request.FromUtc is not null && request.ToUtc is not null && request.ToUtc <= request.FromUtc) throw new ArgumentException("ToUtc must be greater than FromUtc.", nameof(request));
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException($"{name} must be UTC.", name);
        return value;
    }

    public static IReadOnlyList<CreateIndexModel<AiDecisionAuditRecord>> CreateIndexModels() =>
    [
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.AuditId), new CreateIndexOptions { Name = "ux_ai_decision_audit_records_audit_id", Unique = true }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.EventId), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_event_id" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.CorrelationId), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_correlation_id" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.CustomerId), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_customer_id" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.SubscriptionId), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_subscription_id" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.EndpointId), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_endpoint_id" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.Environment), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_environment" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.AgentName), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_agent_name" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.DecisionType), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_decision_type" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.RiskLevel), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_risk_level" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.ConfidenceLevel), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_confidence_level" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.ApprovalStatus), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_approval_status" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.SafeModeDecision), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_safe_mode_decision" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.UsedFallback), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_used_fallback" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.PromptName), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_prompt_name" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.PromptVersion), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_prompt_version" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Descending(r => r.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_created_at_utc_desc" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.EventId).Descending(r => r.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_event_id_created_at_desc" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.CustomerId).Descending(r => r.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_customer_id_created_at_desc" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.DecisionType).Descending(r => r.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_decision_type_created_at_desc" }),
        new(Builders<AiDecisionAuditRecord>.IndexKeys.Ascending(r => r.RiskLevel).Descending(r => r.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_decision_audit_records_risk_level_created_at_desc" })
    ];
}
