using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class RetryAgentResult
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
    public string? TargetUrl { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string? FailureReason { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public double? EndpointRiskScore { get; set; }
    public string? EndpointHealthStatus { get; set; }
    public long? PayloadSizeBytes { get; set; }

    [BsonRepresentation(BsonType.String)]
    public RetryAgentDecision RetryDecision { get; set; }
    public int RetryDelaySeconds { get; set; }
    public int MaxAllowedRetries { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)]
    public List<RetryAgentReasonCode> ReasonCodes { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public bool Fallback { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? LastRetryAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime FailedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static RetryAgentResult FromResponse(RetryAgentResponseDto response, RetryAgentRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        TargetUrl = request.TargetUrl,
        HttpMethod = request.HttpMethod,
        StatusCode = request.StatusCode,
        FailureReason = request.FailureReason,
        ErrorMessage = request.ErrorMessage,
        RetryCount = request.RetryCount,
        MaxRetryCount = request.MaxRetryCount,
        LastRetryAtUtc = request.LastRetryAtUtc,
        FailedAtUtc = request.FailedAtUtc,
        EndpointRiskScore = request.EndpointRiskScore,
        EndpointHealthStatus = request.EndpointHealthStatus,
        PayloadSizeBytes = request.PayloadSizeBytes,
        RetryDecision = response.RetryDecision,
        RetryDelaySeconds = response.RetryDelaySeconds,
        MaxAllowedRetries = response.MaxAllowedRetries,
        RiskLevel = response.RiskLevel,
        RequiresApproval = response.RequiresApproval,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        ReasonCodes = response.ReasonCodes,
        ConfidenceScore = response.ConfidenceScore,
        GeneratedAtUtc = response.GeneratedAtUtc,
        Fallback = response.Fallback
    };
}
