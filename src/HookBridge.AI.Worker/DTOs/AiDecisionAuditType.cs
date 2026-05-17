namespace HookBridge.AI.Worker.DTOs;

public enum AiDecisionAuditType
{
    Unknown = 0,
    RetryDecision,
    SecurityDecision,
    TransformationDecision,
    ObservabilityDecision,
    OrchestrationDecision,
    AutoRemediationRecommendation,
    AnomalyDetection,
    EndpointRiskScore,
    PayloadSchemaDetection,
    JsonToDtoSuggestion,
    ValidationRuleGeneration,
    NaturalLanguageQuery,
    HumanApproval,
    SafeModeEvaluation,
    FallbackDecision
}
