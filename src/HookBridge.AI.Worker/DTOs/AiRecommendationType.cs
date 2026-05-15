using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiRecommendationType>))]
public enum AiRecommendationType
{
    RetryRecommendation = 0,
    DeadLetterRecommendation = 1,
    EndpointRiskRecommendation = 2,
    SecurityRecommendation = 3,
    TransformationRecommendation = 4,
    ValidationRuleRecommendation = 5,
    DtoSuggestion = 6,
    AnomalyRecommendation = 7,
    LogSummaryRecommendation = 8,
    NaturalLanguageRecommendation = 9
}
