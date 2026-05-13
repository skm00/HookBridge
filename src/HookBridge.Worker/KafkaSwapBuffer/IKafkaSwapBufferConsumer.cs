using Confluent.Kafka;

namespace HookBridge.Worker.KafkaSwapBuffer;

public interface IKafkaSwapBufferConsumer : IDisposable
{
    IReadOnlyCollection<TopicPartition> Assignment { get; }

    void Subscribe(string topic);

    ConsumeResult<string, WebhookEvent>? Consume(TimeSpan timeout);

    void Commit(IEnumerable<TopicPartitionOffset> offsets);

    void Pause(IEnumerable<TopicPartition> partitions);

    void Resume(IEnumerable<TopicPartition> partitions);
}
