namespace HookBridge.AI.Worker.DTOs;

public sealed class PayloadSchemaDetectionResponseDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string DetectedSchemaName { get; set; } = string.Empty;
    public string DetectedEventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<PayloadFieldInsightDto> ImportantFields { get; set; } = Array.Empty<PayloadFieldInsightDto>();
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ValidationIssues { get; set; } = Array.Empty<string>();
    public string SuggestedDtoName { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public AiFallbackMetadataDto? Fallback { get; set; }
}
