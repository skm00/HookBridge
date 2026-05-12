using Confluent.Kafka;

namespace HookBridge.Worker.KafkaSwapBuffer;

public sealed class KafkaSwapBufferConsumerFactory : IKafkaSwapBufferConsumerFactory
{
    public IKafkaSwapBufferConsumer Create(KafkaConsumerOptions options)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = false,
        };

        var consumer = new ConsumerBuilder<string, WebhookEvent>(config)
            .SetValueDeserializer(new JsonDeserializer<WebhookEvent>())
            .Build();

        return new KafkaSwapBufferConsumer(consumer);
    }

    private sealed class KafkaSwapBufferConsumer : IKafkaSwapBufferConsumer
    {
        private readonly IConsumer<string, WebhookEvent> _consumer;

        public KafkaSwapBufferConsumer(IConsumer<string, WebhookEvent> consumer)
        {
            _consumer = consumer;
        }

        public IReadOnlyCollection<TopicPartition> Assignment => _consumer.Assignment;

        public void Subscribe(string topic) => _consumer.Subscribe(topic);

        public ConsumeResult<string, WebhookEvent>? Consume(TimeSpan timeout) => _consumer.Consume(timeout);

        public void Commit(IEnumerable<TopicPartitionOffset> offsets) => _consumer.Commit(offsets);

        public void Pause(IEnumerable<TopicPartition> partitions) => _consumer.Pause(partitions);

        public void Resume(IEnumerable<TopicPartition> partitions) => _consumer.Resume(partitions);

        public void Dispose()
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }
}
