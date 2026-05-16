using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HookBridge.AI.Worker.DTOs;

public sealed class TransformationAgentRequestDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public object? SourcePayload { get; set; }
    public object? TargetSchema { get; set; }
    public object? TargetSamplePayload { get; set; }
    public object? ExistingMappingRules { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(EventId)) yield return new ValidationResult("EventId is required.", [nameof(EventId)]);
        if (SourcePayload is null || SourcePayload is string sourceString && string.IsNullOrWhiteSpace(sourceString)) yield return new ValidationResult("SourcePayload is required.", [nameof(SourcePayload)]);
        if (ReceivedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("ReceivedAtUtc must be UTC.", [nameof(ReceivedAtUtc)]);
        if (TargetSchema is null && TargetSamplePayload is null) yield return new ValidationResult("TargetSchema or TargetSamplePayload should be provided.", [nameof(TargetSchema), nameof(TargetSamplePayload)]);
        if (SourcePayload is not null && !CanParseJson(SourcePayload)) yield return new ValidationResult("SourcePayload must be valid JSON.", [nameof(SourcePayload)]);
    }

    private static bool CanParseJson(object value)
    {
        try
        {
            _ = JsonNode.Parse(value switch
            {
                string s => s,
                JsonElement e => e.GetRawText(),
                JsonNode n => n.ToJsonString(),
                _ => JsonSerializer.Serialize(value)
            });
            return true;
        }
        catch (JsonException) { return false; }
    }
}
