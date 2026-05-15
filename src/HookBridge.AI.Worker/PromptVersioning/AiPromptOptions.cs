using System.Text.RegularExpressions;

namespace HookBridge.AI.Worker.PromptVersioning;

public sealed class AiPromptOptions
{
    public const string SectionName = "AIPrompts";
    public const string DefaultPromptVersion = "v1.0.0";

    private static readonly Regex SemanticVersionRegex = new("^v\\d+\\.\\d+\\.\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string DefaultVersion { get; set; } = DefaultPromptVersion;

    public Dictionary<string, string> Prompts { get; set; } = new(StringComparer.Ordinal)
    {
        [AiPromptNames.WebhookFailureAnalysis] = DefaultPromptVersion,
        [AiPromptNames.AiLogSummary] = DefaultPromptVersion,
        [AiPromptNames.PayloadSchemaDetection] = DefaultPromptVersion,
        [AiPromptNames.JsonToDtoSuggestion] = DefaultPromptVersion,
        [AiPromptNames.FluentValidationRuleGeneration] = DefaultPromptVersion,
        [AiPromptNames.WebhookTransformationRecommendation] = DefaultPromptVersion,
        [AiPromptNames.AiSecurityAnalysis] = DefaultPromptVersion,
        [AiPromptNames.NaturalLanguageQuery] = DefaultPromptVersion
    };

    public static bool IsValidVersion(string version) => !string.IsNullOrWhiteSpace(version) && SemanticVersionRegex.IsMatch(version);
}
