namespace HookBridge.AI.Worker.Kafka;

/// <summary>
/// Result returned after attempting to publish an AI analysis event to Kafka.
/// </summary>
public sealed record AiAnalysisPublishResult(
    bool IsSuccess,
    string Topic,
    string? Key,
    string? ErrorMessage = null,
    int? Partition = null,
    long? Offset = null)
{
    public static AiAnalysisPublishResult Success(string topic, string? key, int partition, long offset)
        => new(true, topic, key, null, partition, offset);

    public static AiAnalysisPublishResult Failure(string topic, string? key, string errorMessage)
        => new(false, topic, key, errorMessage);
}
