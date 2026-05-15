namespace HookBridge.Application.DTOs.AiNaturalLanguageQuery;

/// <summary>
/// Response for a safe natural language webhook investigation query.
/// </summary>
public sealed class AiNaturalLanguageQueryResponseDto
{
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public AiNaturalLanguageQueryIntent Intent { get; set; }
    public Dictionary<string, object?> FiltersUsed { get; set; } = [];
    public IReadOnlyList<AiNaturalLanguageQueryResultDto> Results { get; set; } = [];
    public IReadOnlyList<string> SuggestedActions { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool Fallback { get; set; }

    public string PromptName { get; set; } = string.Empty;

    public string PromptVersion { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;
}
