using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonUnknownAiDecisionEventTypeConverter))]
public enum AiDecisionEventType
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
    FallbackDecision,
    DeadLetterAnalysis
}
