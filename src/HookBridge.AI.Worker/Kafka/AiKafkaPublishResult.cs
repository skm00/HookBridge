namespace HookBridge.AI.Worker.Kafka;

/// <summary>
/// Result returned after attempting to publish an AI Kafka event.
/// </summary>
public sealed record AiKafkaPublishResult(
    bool IsSuccess,
    string Topic,
    string? Key,
    int? Partition,
    long? Offset,
    string? ErrorMessage,
    DateTime PublishedAtUtc)
{
    public static AiKafkaPublishResult Success(string topic, string? key, int partition, long offset, DateTime publishedAtUtc)
        => new(true, topic, key, partition, offset, null, publishedAtUtc);

    public static AiKafkaPublishResult Failure(string topic, string? key, string errorMessage, DateTime publishedAtUtc)
        => new(false, topic, key, null, null, errorMessage, publishedAtUtc);
}
