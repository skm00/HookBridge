using HookBridge.AI.Worker.Kafka;

namespace HookBridge.AI.Worker.Configuration;

/// <summary>
/// Kafka connection, topic, and consumer settings for AI analysis events.
/// </summary>
public sealed class AiKafkaOptions
{
    public const string SectionName = "AiKafka";

    public string BootstrapServers { get; set; } = string.Empty;

    public string SecurityProtocol { get; set; } = string.Empty;

    public string SaslMechanism { get; set; } = string.Empty;

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }

    public string AiAnalysisTopic { get; set; } = AiKafkaTopics.Analysis;

    public string PayloadSchemaDetectionTopic { get; set; } = AiKafkaTopics.SchemaDetection;

    public string JsonToDtoSuggestionTopic { get; set; } = AiKafkaTopics.DtoSuggestion;

    public string FluentValidationRuleGenerationTopic { get; set; } = AiKafkaTopics.ValidationRuleGeneration;

    public string WebhookTransformationRecommendationTopic { get; set; } = AiKafkaTopics.TransformationRecommendation;

    public string CustomerEndpointRiskScoreTopic { get; set; } = AiKafkaTopics.EndpointRiskScore;

    public string WebhookFailureAnomalyDetectionTopic { get; set; } = AiKafkaTopics.FailureAnomalies;

    public string AnomaliesTopic { get; set; } = AiKafkaTopics.Anomalies;

    public string SecurityAnalysisTopic { get; set; } = AiKafkaTopics.SecurityAnalysis;

    public string DuplicateReplayDetectionTopic { get; set; } = AiKafkaTopics.DuplicateReplayDetection;

    public string OrchestrationTopic { get; set; } = AiKafkaTopics.Orchestration;

    public string RetryAgentTopic { get; set; } = AiKafkaTopics.RetryAgent;

    public string ConsumerGroupId { get; set; } = string.Empty;

    public bool EnableAutoCommit { get; set; }
}
