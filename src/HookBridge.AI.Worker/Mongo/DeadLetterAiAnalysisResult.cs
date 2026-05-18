using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class DeadLetterAiAnalysisResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string DeadLetterId { get; set; } = string.Empty;
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
    public int? StatusCode { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public bool IsSuspicious { get; set; }
    public bool IsReplay { get; set; }
    public bool IsDuplicate { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)] public DeadLetterReplaySafety ReplaySafety { get; set; }
    [BsonRepresentation(BsonType.String)] public DeadLetterSuggestedAction SuggestedAction { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public double ConfidenceScore { get; set; }
    [BsonRepresentation(BsonType.String)] public AiConfidenceLevel ConfidenceLevel { get; set; }
    public bool RequiresApproval { get; set; }
    [BsonRepresentation(BsonType.String)] public AiSafeModeDecision SafeModeDecision { get; set; }
    public bool IsActionAllowed { get; set; }
    [BsonRepresentation(BsonType.String)] public List<DeadLetterReasonCode> ReasonCodes { get; set; } = [];
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public bool UsedFallback { get; set; }
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? PromptHash { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }

    public static DeadLetterAiAnalysisResult FromResponse(DeadLetterAiAnalysisResponseDto response, DeadLetterAiAnalysisRequestDto request) => new()
    {
        DeadLetterId = response.DeadLetterId,
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
        StatusCode = request.StatusCode,
        FailureReason = request.FailureReason,
        RetryCount = request.RetryCount,
        MaxRetryCount = request.MaxRetryCount,
        IsSuspicious = request.IsSuspicious,
        IsReplay = request.IsReplay,
        IsDuplicate = request.IsDuplicate,
        RootCause = response.RootCause,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        ReplaySafety = response.ReplaySafety,
        SuggestedAction = response.SuggestedAction,
        RiskLevel = response.RiskLevel,
        ConfidenceScore = response.ConfidenceScore,
        ConfidenceLevel = response.ConfidenceLevel,
        RequiresApproval = response.RequiresApproval,
        SafeModeDecision = response.SafeModeDecision,
        IsActionAllowed = response.IsActionAllowed,
        ReasonCodes = response.ReasonCodes.ToList(),
        Model = response.Model,
        Provider = response.Provider,
        UsedFallback = response.Fallback.UsedFallback,
        PromptName = response.PromptName,
        PromptVersion = response.PromptVersion,
        PromptHash = response.PromptHash,
        CreatedAtUtc = DateTime.UtcNow,
        GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc)
    };

    public DeadLetterAiAnalysisResponseDto ToResponseDto() => new()
    {
        DeadLetterId = DeadLetterId,
        EventId = EventId,
        CorrelationId = CorrelationId,
        RootCause = RootCause,
        Summary = Summary,
        Recommendation = Recommendation,
        ReplaySafety = ReplaySafety,
        SuggestedAction = SuggestedAction,
        RiskLevel = RiskLevel,
        ConfidenceScore = ConfidenceScore,
        ConfidenceLevel = ConfidenceLevel,
        RequiresApproval = RequiresApproval,
        SafeModeDecision = SafeModeDecision,
        IsActionAllowed = IsActionAllowed,
        ReasonCodes = ReasonCodes,
        GeneratedAtUtc = GeneratedAtUtc,
        Model = Model,
        Provider = Provider,
        Fallback = new AiFallbackMetadataDto { UsedFallback = UsedFallback, Provider = Provider ?? string.Empty, Model = Model ?? string.Empty, GeneratedAtUtc = GeneratedAtUtc },
        PromptName = PromptName,
        PromptVersion = PromptVersion,
        PromptHash = PromptHash
    };
}
