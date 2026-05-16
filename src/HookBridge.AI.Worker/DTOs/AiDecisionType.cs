namespace HookBridge.AI.Worker.DTOs;

public enum AiDecisionType
{
    Unknown,
    RetryDecision,
    SecurityDecision,
    TransformationDecision,
    ObservabilityDecision,
    OrchestrationDecision,
    AnomalyDecision,
    EndpointRiskDecision,
    PayloadSchemaDecision,
    JsonToDtoDecision,
    ValidationRuleDecision,
    NaturalLanguageAnswer
}
