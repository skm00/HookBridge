using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class CustomerEndpointRiskScoreResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [BsonElement("customerIdType")]
    public string? CustomerIdType { get; set; }

    [BsonElement("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [BsonElement("endpointId")]
    public string? EndpointId { get; set; }

    [BsonElement("targetUrl")]
    public string? TargetUrl { get; set; }

    [BsonElement("environment")]
    public string? Environment { get; set; }

    [BsonElement("riskScore")]
    public int RiskScore { get; set; }

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = AiRiskLevel.Unknown.ToString();

    [BsonElement("healthStatus")]
    public string HealthStatus { get; set; } = EndpointHealthStatus.Unknown.ToString();

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [BsonElement("riskFactors")]
    public List<CustomerEndpointRiskFactorDto> RiskFactors { get; set; } = [];

    [BsonElement("calculatedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CalculatedAtUtc { get; set; }

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CustomerEndpointRiskScoreResult FromResponse(CustomerEndpointRiskScoreResponseDto response)
        => new()
        {
            CustomerId = response.CustomerId,
            CustomerIdType = response.CustomerIdType,
            SubscriptionId = response.SubscriptionId,
            EndpointId = response.EndpointId,
            TargetUrl = response.TargetUrl,
            Environment = response.Environment,
            RiskScore = response.RiskScore,
            RiskLevel = response.RiskLevel.ToString(),
            HealthStatus = response.HealthStatus.ToString(),
            Summary = response.Summary,
            Recommendation = response.Recommendation,
            RiskFactors = response.RiskFactors.ToList(),
            CalculatedAtUtc = DateTime.SpecifyKind(response.CalculatedAtUtc, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.UtcNow
        };
}
