using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.Worker.KafkaSwapBuffer;

/// <summary>
/// MongoDB document that couples a webhook event with the Kafka offset that may be committed only after persistence.
/// </summary>
public sealed class BufferedWebhookEvent
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    public required string EventId { get; init; }

    public required WebhookEvent Event { get; init; }

    public required string Topic { get; init; }

    public required int Partition { get; init; }

    public required long Offset { get; init; }

    public DateTime BufferedAtUtc { get; init; } = DateTime.UtcNow;

    public static BufferedWebhookEvent FromKafka(WebhookEvent @event, string topic, int partition, long offset)
    {
        return new BufferedWebhookEvent
        {
            EventId = @event.EventId,
            Event = @event,
            Topic = topic,
            Partition = partition,
            Offset = offset,
        };
    }
}
