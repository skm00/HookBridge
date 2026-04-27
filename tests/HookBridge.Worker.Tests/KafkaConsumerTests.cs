using Confluent.Kafka;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class KafkaConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_ThrowsWhenTopicIsEmpty()
    {
        var consumer = CreateKafkaConsumer();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync<WebhookEventMessage>(string.Empty, "group-1"))
            {
            }
        });
    }

    [Fact]
    public async Task ConsumeAsync_ThrowsWhenGroupIdIsEmpty()
    {
        var consumer = CreateKafkaConsumer();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync<WebhookEventMessage>("webhook-events", string.Empty))
            {
            }
        });
    }

    [Fact]
    public async Task ConsumeAsync_HandlesInvalidJsonAndContinues()
    {
        var kafkaClientMock = new Mock<IConsumer<string, string>>();
        kafkaClientMock.Setup(c => c.Subscribe("webhook-events"));

        var invalid = BuildResult("webhook-events", "tenant-1", "{invalid-json}", 0, 1);
        var valid = BuildResult(
            "webhook-events",
            "tenant-1",
            "{\"eventId\":\"evt-1\",\"tenantId\":\"tenant-1\",\"eventType\":\"order.created\",\"correlationId\":\"corr-1\"}",
            0,
            2);

        kafkaClientMock.SetupSequence(c => c.Consume(It.IsAny<CancellationToken>()))
            .Returns(invalid)
            .Returns(valid)
            .Throws(new OperationCanceledException());

        var logger = new TestLogger<KafkaConsumer>();
        var consumer = CreateKafkaConsumer(
            logger,
            _ => kafkaClientMock.Object,
            enableAutoCommit: true);

        var results = new List<WebhookEventMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await foreach (var message in consumer.ConsumeAsync<WebhookEventMessage>("webhook-events", "group-1", cts.Token))
        {
            results.Add(message);
            break;
        }

        Assert.Single(results);
        Assert.Equal("evt-1", results[0].EventId);
        Assert.Contains(logger.Records, record => record.Message.Contains("Invalid Kafka message payload."));
    }

    [Fact]
    public async Task ConsumeAsync_RespectsCancellationToken()
    {
        var kafkaClientMock = new Mock<IConsumer<string, string>>();
        kafkaClientMock.Setup(c => c.Subscribe("webhook-events"));
        kafkaClientMock
            .Setup(c => c.Consume(It.IsAny<CancellationToken>()))
            .Throws((CancellationToken token) => new OperationCanceledException(token));

        var consumer = CreateKafkaConsumer(
            consumerFactory: _ => kafkaClientMock.Object,
            enableAutoCommit: true);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var enumerator = consumer.ConsumeAsync<WebhookEventMessage>("webhook-events", "group-1", cts.Token)
            .GetAsyncEnumerator(cts.Token);

        var hasMessage = await enumerator.MoveNextAsync();
        await enumerator.DisposeAsync();

        Assert.False(hasMessage);
    }

    private static KafkaConsumer CreateKafkaConsumer(
        TestLogger<KafkaConsumer>? logger = null,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null,
        bool enableAutoCommit = false)
    {
        var settings = Options.Create(new KafkaSettings
        {
            BootstrapServers = "localhost:9092",
            SecurityProtocol = "Plaintext",
            SaslMechanism = string.Empty,
            ConsumerGroupId = "hookbridge-worker",
            EnableAutoCommit = enableAutoCommit,
            MessageTimeoutMs = 10000,
        });

        return new KafkaConsumer(settings, logger ?? new TestLogger<KafkaConsumer>(), consumerFactory);
    }

    private static ConsumeResult<string, string> BuildResult(
        string topic,
        string key,
        string value,
        int partition,
        long offset)
    {
        return new ConsumeResult<string, string>
        {
            TopicPartitionOffset = new TopicPartitionOffset(topic, new Partition(partition), new Offset(offset)),
            Message = new Message<string, string>
            {
                Key = key,
                Value = value,
            },
        };
    }
}
