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
    public const string DefaultWebhookTransformationRecommendationResultsCollectionName = "webhook_transformation_recommendation_results";
    public const string DefaultCustomerEndpointRiskScoreResultsCollectionName = "customer_endpoint_risk_score_results";
    public const string DefaultWebhookFailureAnomalyDetectionResultsCollectionName = "webhook_failure_anomaly_detection_results";
    public const string DefaultAiAnomalyRecordsCollectionName = "ai_anomaly_records";
    public const string DefaultAiSecurityAnalysisResultsCollectionName = "ai_security_analysis_results";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string AiAnalysisResultsCollectionName { get; set; } = DefaultAiAnalysisResultsCollectionName;

    public string PayloadSchemaDetectionResultsCollectionName { get; set; } = DefaultPayloadSchemaDetectionResultsCollectionName;

    public string JsonToDtoSuggestionResultsCollectionName { get; set; } = DefaultJsonToDtoSuggestionResultsCollectionName;

    public string FluentValidationRuleGenerationResultsCollectionName { get; set; } = DefaultFluentValidationRuleGenerationResultsCollectionName;

    public string WebhookTransformationRecommendationResultsCollectionName { get; set; } = DefaultWebhookTransformationRecommendationResultsCollectionName;

    public string CustomerEndpointRiskScoreResultsCollectionName { get; set; } = DefaultCustomerEndpointRiskScoreResultsCollectionName;

    public string WebhookFailureAnomalyDetectionResultsCollectionName { get; set; } = DefaultWebhookFailureAnomalyDetectionResultsCollectionName;

    public string AiAnomalyRecordsCollectionName { get; set; } = DefaultAiAnomalyRecordsCollectionName;

    public string AiSecurityAnalysisResultsCollectionName { get; set; } = DefaultAiSecurityAnalysisResultsCollectionName;
}
