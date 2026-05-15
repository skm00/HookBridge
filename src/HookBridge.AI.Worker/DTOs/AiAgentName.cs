using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiAgentName>))]
public enum AiAgentName
{
    RetryRecommendationAgent,
    SecurityAnalysisAgent,
    DuplicateReplayDetectionAgent,
    PayloadSchemaDetectionAgent,
    EndpointRiskScoringAgent,
    AnomalyDetectionAgent,
    LogSummarizationAgent,
    TransformationRecommendationAgent
}
