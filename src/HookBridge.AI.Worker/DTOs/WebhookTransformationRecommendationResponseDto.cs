namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookTransformationRecommendationResponseDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<WebhookFieldMappingRecommendationDto> RecommendedMappings { get; set; } = Array.Empty<WebhookFieldMappingRecommendationDto>();
    public IReadOnlyList<string> MissingTargetFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UnmappedSourceFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TransformationNotes { get; set; } = Array.Empty<string>();
    public string GeneratedTransformationCode { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public DateTime GeneratedAtUtc { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public AiFallbackMetadataDto? Fallback { get; set; }
}
