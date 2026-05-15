using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class SecurityAgentResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? TargetUrl { get; set; }
    public string? HttpMethod { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public bool SignatureValidationFailed { get; set; }
    public bool AuthenticationFailed { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsReplay { get; set; }
    public long PayloadSizeBytes { get; set; }
    public bool IsSuspicious { get; set; }
    [BsonRepresentation(BsonType.String)] public SecurityAgentDecision SecurityDecision { get; set; }
    public int SecurityRiskScore { get; set; }
    [BsonRepresentation(BsonType.String)] public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<AiSecuritySignalDto> SecuritySignals { get; set; } = [];
    [BsonRepresentation(BsonType.String)] public List<SecurityAgentReasonCode> ReasonCodes { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public bool Fallback { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ReceivedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SecurityAgentResult FromResponse(SecurityAgentResponseDto response, SecurityAgentRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        Source = request.Source,
        TargetUrl = request.TargetUrl,
        HttpMethod = request.HttpMethod,
        SourceIp = request.SourceIp,
        UserAgent = request.UserAgent,
        SignatureValidationFailed = request.SignatureValidationFailed,
        AuthenticationFailed = request.AuthenticationFailed,
        IsDuplicate = request.IsDuplicate,
        IsReplay = request.IsReplay,
        PayloadSizeBytes = request.PayloadSizeBytes,
        ReceivedAtUtc = request.ReceivedAtUtc,
        IsSuspicious = response.IsSuspicious,
        SecurityDecision = response.SecurityDecision,
        SecurityRiskScore = response.SecurityRiskScore,
        RiskLevel = response.RiskLevel,
        RequiresApproval = response.RequiresApproval,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        SecuritySignals = response.SecuritySignals.ToList(),
        ReasonCodes = response.ReasonCodes.ToList(),
        ConfidenceScore = response.ConfidenceScore,
        GeneratedAtUtc = response.GeneratedAtUtc,
        Fallback = response.Fallback
    };
}
