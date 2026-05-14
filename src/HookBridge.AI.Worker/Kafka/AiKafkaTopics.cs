namespace HookBridge.AI.Worker.Kafka;

/// <summary>
/// Kafka topic names used by the HookBridge AI worker.
/// </summary>
public static class AiKafkaTopics
{
    public const string Analysis = "hookbridge.ai.analysis";

    public const string SchemaDetection = "hookbridge.ai.schema-detection";

    public const string DtoSuggestion = "hookbridge.ai.dto-suggestion";

    public const string ValidationRuleGeneration = "hookbridge.ai.validation-rule-generation";
}
