namespace HookBridge.AI.Worker.DTOs;

public sealed class JsonToDtoSuggestionResponseDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string SuggestedRootClassName { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string GeneratedCode { get; set; } = string.Empty;
    public IReadOnlyList<SuggestedDtoClassDto> Classes { get; set; } = Array.Empty<SuggestedDtoClassDto>();
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> ValidationNotes { get; set; } = Array.Empty<string>();
    public double ConfidenceScore { get; set; }
    public AiConfidenceLevel ConfidenceLevel { get; set; } = AiConfidenceLevel.Unknown;
    public string ConfidenceExplanation { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Unknown";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public AiFallbackMetadataDto? Fallback { get; set; }

    public string PromptName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;
}
