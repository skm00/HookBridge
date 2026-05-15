namespace HookBridge.AI.Worker.PromptVersioning;

public static class AiPromptNames
{
    public const string WebhookFailureAnalysis = nameof(WebhookFailureAnalysis);
    public const string AiLogSummary = nameof(AiLogSummary);
    public const string PayloadSchemaDetection = nameof(PayloadSchemaDetection);
    public const string JsonToDtoSuggestion = nameof(JsonToDtoSuggestion);
    public const string FluentValidationRuleGeneration = nameof(FluentValidationRuleGeneration);
    public const string WebhookTransformationRecommendation = nameof(WebhookTransformationRecommendation);
    public const string AiSecurityAnalysis = nameof(AiSecurityAnalysis);
    public const string NaturalLanguageQuery = nameof(NaturalLanguageQuery);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        WebhookFailureAnalysis,
        AiLogSummary,
        PayloadSchemaDetection,
        JsonToDtoSuggestion,
        FluentValidationRuleGeneration,
        WebhookTransformationRecommendation,
        AiSecurityAnalysis,
        NaturalLanguageQuery
    };

    public static bool IsKnown(string promptName) => All.Contains(promptName);
}
