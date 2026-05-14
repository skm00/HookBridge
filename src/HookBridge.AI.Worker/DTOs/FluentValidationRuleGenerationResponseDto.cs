namespace HookBridge.AI.Worker.DTOs;

public sealed class FluentValidationRuleGenerationResponseDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string ValidatorClassName { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string GeneratedValidatorCode { get; set; } = string.Empty;
    public IReadOnlyList<SuggestedValidationRuleDto> Rules { get; set; } = Array.Empty<SuggestedValidationRuleDto>();
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> ValidationNotes { get; set; } = Array.Empty<string>();
    public double ConfidenceScore { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public DateTime GeneratedAtUtc { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public AiFallbackMetadataDto? Fallback { get; set; }
}
