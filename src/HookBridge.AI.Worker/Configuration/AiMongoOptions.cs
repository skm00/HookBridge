namespace HookBridge.AI.Worker.Configuration;

/// <summary>
/// MongoDB settings for persisting AI analysis results.
/// </summary>
public sealed class AiMongoOptions
{
    public const string SectionName = "AiMongo";
    public const string DefaultAiAnalysisResultsCollectionName = "ai_analysis_results";
    public const string DefaultPayloadSchemaDetectionResultsCollectionName = "payload_schema_detection_results";
    public const string DefaultJsonToDtoSuggestionResultsCollectionName = "json_to_dto_suggestion_results";
    public const string DefaultFluentValidationRuleGenerationResultsCollectionName = "fluent_validation_rule_generation_results";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string AiAnalysisResultsCollectionName { get; set; } = DefaultAiAnalysisResultsCollectionName;

    public string PayloadSchemaDetectionResultsCollectionName { get; set; } = DefaultPayloadSchemaDetectionResultsCollectionName;

    public string JsonToDtoSuggestionResultsCollectionName { get; set; } = DefaultJsonToDtoSuggestionResultsCollectionName;

    public string FluentValidationRuleGenerationResultsCollectionName { get; set; } = DefaultFluentValidationRuleGenerationResultsCollectionName;
}
