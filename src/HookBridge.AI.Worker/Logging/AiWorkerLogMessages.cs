namespace HookBridge.AI.Worker.Logging;

public static class AiWorkerLogMessages
{
    public const string WorkerStarting = "HookBridge AI Worker starting. Enabled: {Enabled}, Provider: {Provider}, Model: {Model}, KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}";
    public const string WorkerAiDisabled = "HookBridge AI Worker AI is disabled. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}";
    public const string WorkerAiEnabled = "HookBridge AI Worker AI enabled. Provider: {Provider}, Model: {Model}, KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}";
    public const string WorkerStopping = "HookBridge AI Worker shutting down.";
    public const string CancellationRequested = "HookBridge AI Worker cancellation requested. Operation: {Operation}";
    public const string ProcessingStarted = "AI analysis processing started. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}";
    public const string ProcessingCompleted = "AI analysis processing completed. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, FallbackUsed: {FallbackUsed}, FallbackReason: {FallbackReason}, RiskLevel: {RiskLevel}, SuggestedRetryAction: {SuggestedRetryAction}, DurationMs: {DurationMs}";
    public const string ProcessingFailed = "AI analysis processing failed. Operation: {Operation}, EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, DurationMs: {DurationMs}";
    public const string FallbackUsed = "AI fallback used. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, FallbackUsed: {FallbackUsed}, FallbackReason: {FallbackReason}, RiskLevel: {RiskLevel}, SuggestedRetryAction: {SuggestedRetryAction}";
    public const string MongoInsertStarted = "Mongo insert started. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}";
    public const string MongoInsertCompleted = "Mongo insert completed. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}";
    public const string MongoInsertFailed = "Mongo insert failed. Operation: {Operation}, EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}";
    public const string KafkaConsumerStarted = "AI analysis Kafka consumer started. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}";
    public const string KafkaMessageReceived = "AI analysis Kafka message received. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}";
    public const string InvalidMessageSkipped = "Invalid AI analysis Kafka message skipped. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}";
    public const string PromptGenerationStarted = "AI prompt generation started. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}";
    public const string PromptGenerationCompleted = "AI prompt generation completed. EventId: {EventId}, CorrelationId: {CorrelationId}, EventType: {EventType}, Source: {Source}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}";
    public const string LlmRequestStarted = "LLM request started. Provider: {Provider}, Model: {Model}, Attempt: {Attempt}, Attempts: {Attempts}";
    public const string LlmRequestCompleted = "LLM request completed. Provider: {Provider}, Model: {Model}, Attempt: {Attempt}, Attempts: {Attempts}, DurationMs: {DurationMs}";
}
