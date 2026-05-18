using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiDecisionAuditRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string AuditId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? AgentName { get; set; }
    [BsonRepresentation(BsonType.String)]
    public AiDecisionAuditType DecisionType { get; set; }
    public string? Decision { get; set; }
    public string? RiskLevel { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? SuggestedAction { get; set; }
    public bool? RequiresApproval { get; set; }
    public string? ApprovalId { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? SafeModeDecision { get; set; }
    public bool? IsActionAllowed { get; set; }
    public bool? UsedAi { get; set; }
    public bool? UsedFallback { get; set; }
    public string? FallbackReason { get; set; }
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? PromptHash { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? Summary { get; set; }
    public string? Recommendation { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public string? CreatedBy { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string?> Metadata { get; set; } = [];

    public AiDecisionAuditResponseDto ToResponseDto() => new()
    {
        Id = Id,
        AuditId = AuditId,
        EventId = EventId,
        CorrelationId = CorrelationId,
        CustomerId = CustomerId,
        CustomerIdType = CustomerIdType,
        SubscriptionId = SubscriptionId,
        EndpointId = EndpointId,
        Environment = Environment,
        AgentName = AgentName,
        DecisionType = DecisionType,
        Decision = Decision,
        RiskLevel = RiskLevel,
        ConfidenceScore = ConfidenceScore,
        ConfidenceLevel = ConfidenceLevel,
        SuggestedAction = SuggestedAction,
        RequiresApproval = RequiresApproval,
        ApprovalId = ApprovalId,
        ApprovalStatus = ApprovalStatus,
        SafeModeDecision = SafeModeDecision,
        IsActionAllowed = IsActionAllowed,
        UsedAi = UsedAi,
        UsedFallback = UsedFallback,
        FallbackReason = FallbackReason,
        PromptName = PromptName,
        PromptVersion = PromptVersion,
        PromptHash = PromptHash,
        Model = Model,
        Provider = Provider,
        Summary = Summary,
        Recommendation = Recommendation,
        ReasonCodes = ReasonCodes,
        CreatedBy = CreatedBy,
        CreatedAtUtc = CreatedAtUtc,
        Metadata = Metadata
    };
}
