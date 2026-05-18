using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Mappers;

public static class AiDecisionEventMapper
{
    public static AiDecisionEventDto FromAuditRecord(AiDecisionAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new AiDecisionEventDto
        {
            DecisionId = string.IsNullOrWhiteSpace(record.DecisionId) ? record.AuditId : record.DecisionId,
            AuditId = record.AuditId,
            EventId = record.EventId,
            CorrelationId = record.CorrelationId,
            CustomerId = record.CustomerId,
            CustomerIdType = record.CustomerIdType,
            SubscriptionId = record.SubscriptionId,
            EndpointId = record.EndpointId,
            Environment = record.Environment,
            AgentName = record.AgentName,
            DecisionType = MapDecisionType(record.DecisionType),
            Decision = record.Decision,
            RiskLevel = record.RiskLevel,
            ConfidenceScore = record.ConfidenceScore,
            ConfidenceLevel = record.ConfidenceLevel,
            SuggestedAction = record.SuggestedAction,
            RequiresApproval = record.RequiresApproval,
            ApprovalId = record.ApprovalId,
            ApprovalStatus = record.ApprovalStatus,
            SafeModeDecision = record.SafeModeDecision,
            IsActionAllowed = record.IsActionAllowed,
            UsedAi = record.UsedAi,
            UsedFallback = record.UsedFallback,
            FallbackReason = record.FallbackReason,
            PromptName = record.PromptName,
            PromptVersion = record.PromptVersion,
            PromptHash = record.PromptHash,
            Model = record.Model,
            Provider = record.Provider,
            Summary = record.Summary,
            Recommendation = record.Recommendation,
            ReasonCodes = record.ReasonCodes ?? [],
            Source = string.IsNullOrWhiteSpace(record.CreatedBy) ? "HookBridge.AI.Worker" : record.CreatedBy!,
            CreatedAtUtc = DateTime.SpecifyKind(record.CreatedAtUtc, DateTimeKind.Utc)
        };
    }

    public static AiDecisionEventDto FromCreateRequest(AiDecisionAuditCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new AiDecisionEventDto
        {
            DecisionId = string.IsNullOrWhiteSpace(request.DecisionId) ? $"dec_{Guid.NewGuid():N}" : request.DecisionId!,
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            AgentName = request.AgentName,
            DecisionType = MapDecisionType(request.DecisionType),
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
            PromptName = request.PromptName,
            PromptVersion = request.PromptVersion,
            PromptHash = request.PromptHash,
            Model = request.Model,
            Provider = request.Provider,
            Summary = request.Summary,
            Recommendation = request.Recommendation,
            ReasonCodes = request.ReasonCodes ?? [],
            Source = string.IsNullOrWhiteSpace(request.CreatedBy) ? "HookBridge.AI.Worker" : request.CreatedBy!,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static AiDecisionEventType MapDecisionType(AiDecisionAuditType type)
        => Enum.TryParse<AiDecisionEventType>(type.ToString(), out var mapped) ? mapped : AiDecisionEventType.Unknown;
}
