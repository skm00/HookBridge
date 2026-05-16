using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiAgentName>))]
public enum AiAgentName
{
    RetryRecommendationAgent,
    SecurityAnalysisAgent,
    SecurityAgent,
    DuplicateReplayDetectionAgent,
    PayloadSchemaDetectionAgent,
    EndpointRiskScoringAgent,
    AnomalyDetectionAgent,
    LogSummarizationAgent,
    TransformationRecommendationAgent,
    TransformationAgent
}
