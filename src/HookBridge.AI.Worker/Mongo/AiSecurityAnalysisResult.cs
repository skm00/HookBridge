using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSecurityAnalysisResult
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
    [BsonElement("source")] public string? Source { get; set; }
    [BsonElement("eventType")] public string? EventType { get; set; }
    [BsonElement("targetUrl")] public string? TargetUrl { get; set; }
    [BsonElement("httpMethod")] public string? HttpMethod { get; set; }
    [BsonElement("sourceIp")] public string? SourceIp { get; set; }
    [BsonElement("userAgent")] public string? UserAgent { get; set; }
    [BsonElement("signatureValidationFailed")] public bool SignatureValidationFailed { get; set; }
    [BsonElement("authenticationFailed")] public bool AuthenticationFailed { get; set; }
    [BsonElement("payloadSizeBytes")] public long PayloadSizeBytes { get; set; }
    [BsonElement("receivedAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ReceivedAtUtc { get; set; }
    [BsonElement("isSuspicious")] public bool IsSuspicious { get; set; }
    [BsonElement("securityRiskScore")] public int SecurityRiskScore { get; set; }
    [BsonElement("riskLevel")] public string RiskLevel { get; set; } = AiRiskLevel.Unknown.ToString();
    [BsonElement("summary")] public string Summary { get; set; } = string.Empty;
    [BsonElement("recommendation")] public string Recommendation { get; set; } = string.Empty;
    [BsonElement("detectedSecuritySignals")] public List<AiSecuritySignalDto> DetectedSecuritySignals { get; set; } = [];
    [BsonElement("suggestedAction")] public string SuggestedAction { get; set; } = AiSecuritySuggestedAction.None.ToString();
    [BsonElement("confidenceScore")] public double ConfidenceScore { get; set; }
    [BsonElement("generatedAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }
    [BsonElement("model")] public string Model { get; set; } = string.Empty;
    [BsonElement("provider")] public string Provider { get; set; } = string.Empty;
    [BsonElement("fallback")] public AiFallbackMetadataDto? Fallback { get; set; }

    [BsonElement("promptName")]
    public string PromptName { get; set; } = string.Empty;

    [BsonElement("promptVersion")]
    public string PromptVersion { get; set; } = string.Empty;

    [BsonElement("promptHash")]
    public string PromptHash { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static AiSecurityAnalysisResult FromResponse(AiSecurityAnalysisResponseDto response, AiSecurityAnalysisRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);
        return new AiSecurityAnalysisResult
        {
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            Source = request.Source,
            EventType = request.EventType,
            TargetUrl = request.TargetUrl,
            HttpMethod = request.HttpMethod,
            SourceIp = request.SourceIp,
            UserAgent = request.UserAgent,
            SignatureValidationFailed = request.SignatureValidationFailed,
            AuthenticationFailed = request.AuthenticationFailed,
            PayloadSizeBytes = request.PayloadSizeBytes,
            ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Utc),
            IsSuspicious = response.IsSuspicious,
            SecurityRiskScore = response.SecurityRiskScore,
            RiskLevel = response.RiskLevel.ToString(),
            Summary = response.Summary,
            Recommendation = response.Recommendation,
            DetectedSecuritySignals = response.DetectedSecuritySignals.ToList(),
            SuggestedAction = response.SuggestedAction.ToString(),
            ConfidenceScore = response.ConfidenceScore,
            GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc),
            Model = response.Model,
            Provider = response.Provider,
            PromptName = response.PromptName,
            PromptVersion = response.PromptVersion,
            PromptHash = response.PromptHash,
            Fallback = response.Fallback,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
