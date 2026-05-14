using System.ComponentModel.DataAnnotations;

namespace HookBridge.Api.Configuration;

/// <summary>
/// Options that constrain natural language AI query lookback windows and answer generation.
/// </summary>
public sealed class AiNaturalLanguageQueryOptions
{
    public const string SectionName = "AiNaturalLanguageQuery";

    [Range(1, 2160)]
    public int DefaultLookbackHours { get; set; } = 24;

    [Range(1, 365)]
    public int MaxLookbackDays { get; set; } = 90;

    [Range(1, 100)]
    public int DefaultMaxResults { get; set; } = 20;

    [Range(1, 100)]
    public int HardMaxResults { get; set; } = 100;

    public bool EnableAiAnswerGeneration { get; set; } = true;
}
