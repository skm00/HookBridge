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

    public const string TransformationRecommendation = "hookbridge.ai.transformation-recommendation";

    public const string EndpointRiskScore = "hookbridge.ai.endpoint-risk-score";

    public const string FailureAnomalies = "hookbridge.ai.failure-anomalies";

    public const string Anomalies = "hookbridge.ai.anomalies";

    public const string SecurityAnalysis = "hookbridge.ai.security-analysis";

    public const string DuplicateReplayDetection = "hookbridge.ai.duplicate-replay-detection";
}
