using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAgentOrchestrationResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("eventId")] public string EventId { get; set; } = string.Empty;
    [BsonElement("correlationId")] public string? CorrelationId { get; set; }
    [BsonElement("customerId")] public string? CustomerId { get; set; }
    [BsonElement("customerIdType")] public string? CustomerIdType { get; set; }
    [BsonElement("subscriptionId")] public string? SubscriptionId { get; set; }
    [BsonElement("endpointId")] public string? EndpointId { get; set; }
    [BsonElement("environment")] public string? Environment { get; set; }
    [BsonElement("eventType")] public string? EventType { get; set; }
    [BsonElement("source")] public string? Source { get; set; }
    [BsonElement("targetUrl")] public string? TargetUrl { get; set; }
    [BsonElement("statusCode")] public int? StatusCode { get; set; }
    [BsonElement("retryCount")] public int RetryCount { get; set; }
    [BsonElement("maxRetryCount")] public int MaxRetryCount { get; set; }
    [BsonElement("receivedAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ReceivedAtUtc { get; set; }
    [BsonElement("overallSummary")] public string OverallSummary { get; set; } = string.Empty;
    [BsonElement("overallRiskLevel")] public string OverallRiskLevel { get; set; } = AiRiskLevel.Unknown.ToString();
    [BsonElement("recommendedAction")] public string RecommendedAction { get; set; } = AiOrchestrationRecommendedAction.None.ToString();
    [BsonElement("confidenceScore")] public double ConfidenceScore { get; set; }
    [BsonElement("agentResults")] public List<AiAgentResultDto> AgentResults { get; set; } = [];
    [BsonElement("requiresApproval")] public bool RequiresApproval { get; set; }
    [BsonElement("approvalId")][BsonIgnoreIfNull] public string? ApprovalId { get; set; }
    [BsonElement("generatedAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }
    [BsonElement("createdAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static AiAgentOrchestrationResult FromResponse(AiAgentOrchestrationResponseDto response, AiAgentOrchestrationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);
        return new AiAgentOrchestrationResult
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
            StatusCode = request.StatusCode,
            RetryCount = request.RetryCount,
            MaxRetryCount = request.MaxRetryCount,
            ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Utc),
            OverallSummary = response.OverallSummary,
            OverallRiskLevel = response.OverallRiskLevel.ToString(),
            RecommendedAction = response.RecommendedAction.ToString(),
            ConfidenceScore = response.ConfidenceScore,
            AgentResults = response.AgentResults.ToList(),
            RequiresApproval = response.RequiresApproval,
            ApprovalId = response.ApprovalId,
            GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public AiAgentOrchestrationResponseDto ToResponseDto() => new()
    {
        EventId = EventId,
        CorrelationId = CorrelationId,
        OverallSummary = OverallSummary,
        OverallRiskLevel = Enum.TryParse<AiRiskLevel>(OverallRiskLevel, out var risk) ? risk : AiRiskLevel.Unknown,
        RecommendedAction = Enum.TryParse<AiOrchestrationRecommendedAction>(RecommendedAction, out var action) ? action : AiOrchestrationRecommendedAction.None,
        ConfidenceScore = ConfidenceScore,
        AgentResults = AgentResults,
        RequiresApproval = RequiresApproval,
        ApprovalId = ApprovalId,
        GeneratedAtUtc = GeneratedAtUtc
    };
}
