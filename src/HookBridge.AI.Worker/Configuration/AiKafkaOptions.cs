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

    public string ConsumerGroupId { get; set; } = string.Empty;

    public bool EnableAutoCommit { get; set; }
}
