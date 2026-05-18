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
    public const string DefaultWebhookEventFingerprintsCollectionName = "webhook_event_fingerprints";
    public const string DefaultAiRecommendationApprovalsCollectionName = "ai_recommendation_approvals";
    public const string DefaultAiAgentOrchestrationResultsCollectionName = "ai_agent_orchestration_results";
    public const string DefaultRetryAgentResultsCollectionName = "retry_agent_results";
    public const string DefaultSecurityAgentResultsCollectionName = "security_agent_results";
    public const string DefaultTransformationAgentResultsCollectionName = "transformation_agent_results";
    public const string DefaultObservabilityAgentResultsCollectionName = "observability_agent_results";
    public const string DefaultAutoRemediationRecommendationResultsCollectionName = "auto_remediation_recommendation_results";
    public const string DefaultAiSafeModeAuditRecordsCollectionName = "ai_safe_mode_audit_records";
    public const string DefaultAiDecisionAuditRecordsCollectionName = "ai_decision_audit_records";
    public const string DefaultDeadLetterAiAnalysisResultsCollectionName = "dead_letter_ai_analysis_results";

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

    public string WebhookEventFingerprintsCollectionName { get; set; } = DefaultWebhookEventFingerprintsCollectionName;

    public string AiRecommendationApprovalsCollectionName { get; set; } = DefaultAiRecommendationApprovalsCollectionName;

    public string AiAgentOrchestrationResultsCollectionName { get; set; } = DefaultAiAgentOrchestrationResultsCollectionName;

    public string RetryAgentResultsCollectionName { get; set; } = DefaultRetryAgentResultsCollectionName;

    public string SecurityAgentResultsCollectionName { get; set; } = DefaultSecurityAgentResultsCollectionName;

    public string TransformationAgentResultsCollectionName { get; set; } = DefaultTransformationAgentResultsCollectionName;

    public string ObservabilityAgentResultsCollectionName { get; set; } = DefaultObservabilityAgentResultsCollectionName;

    public string AutoRemediationRecommendationResultsCollectionName { get; set; } = DefaultAutoRemediationRecommendationResultsCollectionName;

    public string AiSafeModeAuditRecordsCollectionName { get; set; } = DefaultAiSafeModeAuditRecordsCollectionName;

    public string AiDecisionAuditRecordsCollectionName { get; set; } = DefaultAiDecisionAuditRecordsCollectionName;

    public string DeadLetterAiAnalysisResultsCollectionName { get; set; } = DefaultDeadLetterAiAnalysisResultsCollectionName;
}
