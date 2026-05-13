namespace HookBridge.Worker.KafkaSwapBuffer;

/// <summary>
/// Configures the swap-buffer Kafka consumer used to persist webhook ingestion streams into MongoDB.
/// </summary>
public sealed class KafkaConsumerOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    public string TopicName { get; set; } = "webhook-events";

    public string MongoCollectionName { get; set; } = "webhook_events";

    public int BatchSize { get; set; } = 500;

    public int FlushIntervalSeconds { get; set; } = 5;

    public int MaxBufferSize { get; set; } = 10_000;

    public bool EnableBackpressure { get; set; } = true;
}
