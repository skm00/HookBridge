using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Audit;

public sealed class AiDecisionAuditService : IAiDecisionAuditService
{
    private static readonly string[] SensitiveKeys = ["Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key", "X-API-Key", "ClientSecret", "AccessToken", "ConnectionString"];
    private static readonly string[] ProhibitedRawKeys = ["Payload", "RawPayload", "Headers", "RawHeaders", "GeneratedCode"];
    private readonly IAiDecisionAuditRepository _repository;
    private readonly AiDecisionAuditOptions _options;
    private readonly ILogger<AiDecisionAuditService> _logger;

    public AiDecisionAuditService(IAiDecisionAuditRepository repository, IOptions<AiDecisionAuditOptions> options, ILogger<AiDecisionAuditService> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public Task<AiDecisionAuditRecord?> AuditRetryDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.RetryDecision, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditSecurityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.SecurityDecision, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditTransformationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.TransformationDecision, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditObservabilityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.ObservabilityDecision, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditOrchestrationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.OrchestrationDecision, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditAutoRemediationRecommendationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, AiDecisionAuditType.AutoRemediationRecommendation, cancellationToken);

    public Task<AiDecisionAuditRecord?> AuditHumanApprovalAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => _options.AuditHumanApprovals ? AuditTypedAsync(request, AiDecisionAuditType.HumanApproval, cancellationToken) : Task.FromResult<AiDecisionAuditRecord?>(null);

    public Task<AiDecisionAuditRecord?> AuditSafeModeEvaluationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => _options.AuditSafeModeEvaluations ? AuditTypedAsync(request, AiDecisionAuditType.SafeModeEvaluation, cancellationToken) : Task.FromResult<AiDecisionAuditRecord?>(null);

    public Task<AiDecisionAuditRecord?> AuditFallbackDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => _options.AuditFallbackDecisions ? AuditTypedAsync(request, AiDecisionAuditType.FallbackDecision, cancellationToken) : Task.FromResult<AiDecisionAuditRecord?>(null);

    public Task<AiDecisionAuditRecord?> AuditGenericDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        => AuditTypedAsync(request, request.DecisionType, cancellationToken);

    private async Task<AiDecisionAuditRecord?> AuditTypedAsync(AiDecisionAuditCreateRequestDto request, AiDecisionAuditType decisionType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled) return null;
        try
        {
            request.DecisionType = decisionType == AiDecisionAuditType.Unknown ? request.DecisionType : decisionType;
            var record = CreateRecord(request);
            await _repository.InsertAsync(record, cancellationToken);
            _logger.LogInformation("AI decision audit record created. AuditId: {AuditId}, EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}, AgentName: {AgentName}", record.AuditId, record.EventId, record.CorrelationId, record.DecisionType, record.AgentName);
            return record;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI decision audit insert failed. EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}, AgentName: {AgentName}", request.EventId, request.CorrelationId, request.DecisionType, request.AgentName);
            return null;
        }
    }

    private AiDecisionAuditRecord CreateRecord(AiDecisionAuditCreateRequestDto request)
    {
        if (request.DecisionType == AiDecisionAuditType.Unknown) throw new ArgumentException("DecisionType is required.", nameof(request));
        if (request.ConfidenceScore is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(request), "ConfidenceScore must be between 0 and 1.");
        var metadata = SanitizeMetadata(request.Metadata, out var sanitized);
        if (sanitized)
        {
            _logger.LogInformation("AI decision audit metadata sanitized. EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}", request.EventId, request.CorrelationId, request.DecisionType);
        }

        return new AiDecisionAuditRecord
        {
            AuditId = $"aud_{Guid.NewGuid():N}",
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            AgentName = request.AgentName,
            DecisionType = request.DecisionType,
            Decision = request.Decision,
            RiskLevel = request.RiskLevel,
            ConfidenceScore = request.ConfidenceScore,
            ConfidenceLevel = request.ConfidenceLevel,
            SuggestedAction = request.SuggestedAction,
            RequiresApproval = request.RequiresApproval,
            ApprovalId = request.ApprovalId,
            ApprovalStatus = request.ApprovalStatus,
            SafeModeDecision = request.SafeModeDecision,
            IsActionAllowed = request.IsActionAllowed,
            UsedAi = request.UsedAi,
            UsedFallback = request.UsedFallback,
            FallbackReason = request.FallbackReason,
            PromptName = _options.IncludePromptMetadata ? request.PromptName : null,
            PromptVersion = _options.IncludePromptMetadata ? request.PromptVersion : null,
            PromptHash = _options.IncludePromptMetadata ? request.PromptHash : null,
            Model = _options.IncludeModelMetadata ? request.Model : null,
            Provider = _options.IncludeModelMetadata ? request.Provider : null,
            Summary = request.Summary,
            Recommendation = request.Recommendation,
            ReasonCodes = request.ReasonCodes ?? [],
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? "HookBridge.AI.Worker" : request.CreatedBy,
            CreatedAtUtc = DateTime.UtcNow,
            Metadata = metadata
        };
    }

    public Dictionary<string, string?> SanitizeMetadata(IReadOnlyDictionary<string, string?>? metadata, out bool sanitized)
    {
        sanitized = false;
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (metadata is null || metadata.Count == 0) return result;

        foreach (var item in metadata)
        {
            if (ProhibitedRawKeys.Any(key => string.Equals(key, item.Key, StringComparison.OrdinalIgnoreCase)))
            {
                sanitized = true;
                continue;
            }

            var value = SensitiveKeys.Any(key => item.Key.Contains(key, StringComparison.OrdinalIgnoreCase)) ? "***MASKED***" : item.Value;
            if (!string.Equals(value, item.Value, StringComparison.Ordinal)) sanitized = true;
            result[item.Key] = value;
        }

        var json = JsonSerializer.Serialize(result);
        if (_options.MaxMetadataLength > 0 && json.Length > _options.MaxMetadataLength)
        {
            sanitized = true;
            var truncated = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var remaining = Math.Max(0, _options.MaxMetadataLength - 80);
            foreach (var item in result)
            {
                if (remaining <= 0) break;
                var value = item.Value;
                if (value is not null && value.Length > remaining)
                {
                    value = value[..remaining];
                }
                truncated[item.Key] = value;
                remaining -= item.Key.Length + (value?.Length ?? 0);
            }
            truncated["metadataTruncated"] = "true";
            return truncated;
        }

        return result;
    }
}
