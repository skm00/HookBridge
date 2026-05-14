using Confluent.Kafka;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnalysisConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_SubscribesToAnalysisTopicAndDeserializesEvents()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Analysis));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("corr-1", "{\"eventId\":\"evt-1\",\"correlationId\":\"corr-1\",\"source\":\"worker\",\"eventType\":\"webhook.delivery.failed\",\"failureReason\":\"HTTP 500\",\"payload\":\"{}\",\"createdAtUtc\":\"2026-05-13T10:15:30+00:00\"}"))
            .Throws(new OperationCanceledException());

        var consumer = CreateConsumer(kafkaClient.Object);

        AiAnalysisEventDto? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            result = message;
            break;
        }

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt-1");
        result.CorrelationId.Should().Be("corr-1");
        result.Source.Should().Be("worker");
        result.EventType.Should().Be("webhook.delivery.failed");
        kafkaClient.Verify(client => client.Subscribe(AiKafkaTopics.Analysis), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_HandlesInvalidJsonSafelyAndContinues()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Analysis));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("corr-invalid", "{not-valid-json"))
            .Returns(BuildResult("corr-valid", "{\"eventId\":\"evt-after-invalid\",\"correlationId\":\"corr-valid\",\"source\":\"worker\",\"eventType\":\"webhook.delivery.failed\",\"payload\":\"{}\",\"createdAtUtc\":\"2026-05-13T10:15:30+00:00\"}"))
            .Throws(new OperationCanceledException());
        var logger = new TestLogger<AiAnalysisConsumer>();
        var consumer = CreateConsumer(kafkaClient.Object, logger);

        AiAnalysisEventDto? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            result = message;
            break;
        }

        result.Should().NotBeNull();
        result!.EventId.Should().Be("evt-after-invalid");
        logger.Records.Should().Contain(record =>
            record.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            record.Message.Contains("Invalid AI analysis Kafka message skipped", StringComparison.Ordinal));
    }


    [Fact]
    public async Task ConsumeAsync_WhenInvalidJson_LogsWarningWithoutPayload()
    {
        const string sensitivePayload = "{\"authorization\":\"secret-token\",\"cookie\":\"session=secret\"";
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Analysis));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("corr-invalid", sensitivePayload))
            .Throws(new OperationCanceledException());
        var logger = new TestLogger<AiAnalysisConsumer>();
        var consumer = CreateConsumer(kafkaClient.Object, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            // Test consumer uses cancellation to stop the async stream after the invalid message.
        }

        logger.Records.Should().Contain(record =>
            record.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            record.Message.Contains("Invalid AI analysis Kafka message skipped", StringComparison.Ordinal));
        logger.Records.Should().NotContain(record =>
            record.Message.Contains("secret-token", StringComparison.Ordinal) ||
            record.Message.Contains("session=secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsumeAsync_RespectsCancellationToken()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Analysis));
        kafkaClient
            .Setup(client => client.Consume(It.IsAny<CancellationToken>()))
            .Throws((CancellationToken token) => new OperationCanceledException(token));
        var consumer = CreateConsumer(kafkaClient.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var hasMessage = await enumerator.MoveNextAsync();
        await enumerator.DisposeAsync();

        hasMessage.Should().BeFalse();
    }

    private static AiAnalysisConsumer CreateConsumer(
        IConsumer<string, string> kafkaClient,
        TestLogger<AiAnalysisConsumer>? logger = null)
        => new(
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                AiAnalysisTopic = AiKafkaTopics.Analysis,
                ConsumerGroupId = "hookbridge-ai-tests",
                EnableAutoCommit = true,
            }),
            logger ?? new TestLogger<AiAnalysisConsumer>(),
            _ => kafkaClient);

    private static ConsumeResult<string, string> BuildResult(string key, string value)
        => new()
        {
            Topic = AiKafkaTopics.Analysis,
            Partition = new Partition(0),
            Offset = new Offset(12),
            Message = new Message<string, string>
            {
                Key = key,
                Value = value,
            },
        };
}
