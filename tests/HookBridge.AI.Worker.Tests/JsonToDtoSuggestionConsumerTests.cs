using Confluent.Kafka;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class JsonToDtoSuggestionConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_WithManualCommit_DefersCommitUntilMessageIsAcknowledged()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        var consumeResult = BuildResult("corr-1", "{\"eventId\":\"evt-1\",\"correlationId\":\"corr-1\",\"payload\":{},\"receivedAtUtc\":\"2026-05-14T10:30:00Z\"}");
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.DtoSuggestion));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(consumeResult)
            .Throws(new OperationCanceledException());

        var consumer = CreateConsumer(kafkaClient.Object, enableAutoCommit: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        var hasMessage = await enumerator.MoveNextAsync();

        hasMessage.Should().BeTrue();
        enumerator.Current.Request.EventId.Should().Be("evt-1");
        kafkaClient.Verify(client => client.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Never);

        await enumerator.Current.AcknowledgeAsync(cts.Token);

        kafkaClient.Verify(client => client.Commit(consumeResult), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_WithAutoCommit_DoesNotCommitOnAcknowledgement()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        var consumeResult = BuildResult("corr-1", "{\"eventId\":\"evt-1\",\"correlationId\":\"corr-1\",\"payload\":{},\"receivedAtUtc\":\"2026-05-14T10:30:00Z\"}");
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.DtoSuggestion));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(consumeResult)
            .Throws(new OperationCanceledException());

        var consumer = CreateConsumer(kafkaClient.Object, enableAutoCommit: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator(cts.Token);

        var hasMessage = await enumerator.MoveNextAsync();
        await enumerator.Current.AcknowledgeAsync(cts.Token);

        hasMessage.Should().BeTrue();
        kafkaClient.Verify(client => client.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Never);
    }

    private static JsonToDtoSuggestionConsumer CreateConsumer(
        IConsumer<string, string> kafkaClient,
        bool enableAutoCommit)
        => new(
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                JsonToDtoSuggestionTopic = AiKafkaTopics.DtoSuggestion,
                ConsumerGroupId = "hookbridge-ai-tests",
                EnableAutoCommit = enableAutoCommit,
            }),
            new TestLogger<JsonToDtoSuggestionConsumer>(),
            _ => kafkaClient);

    private static ConsumeResult<string, string> BuildResult(string key, string value)
        => new()
        {
            Topic = AiKafkaTopics.DtoSuggestion,
            Partition = new Partition(0),
            Offset = new Offset(12),
            Message = new Message<string, string>
            {
                Key = key,
                Value = value,
            },
        };
}
