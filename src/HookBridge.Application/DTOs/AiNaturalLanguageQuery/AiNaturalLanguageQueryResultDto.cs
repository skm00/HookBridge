using System.Text.Json.Serialization;

namespace HookBridge.Application.DTOs.AiNaturalLanguageQuery;

/// <summary>
/// Safe, redacted result returned for a natural language query.
/// </summary>
public sealed class AiNaturalLanguageQueryResultDto
{
    public string? Id { get; set; }
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }

    [JsonIgnore]
    public string? CustomerIdType { get; set; }

    [JsonIgnore]
    public string? Environment { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string ResultType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
