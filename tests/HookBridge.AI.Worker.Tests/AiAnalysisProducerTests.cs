using Confluent.Kafka;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnalysisProducerTests
{
    [Fact]
    public async Task PublishAsync_SerializesMessageAsJsonAndPublishesToAnalysisTopic()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        Message<string, string>? published = null;
        producerClient
            .Setup(client => client.ProduceAsync(AiKafkaTopics.Analysis, It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => published = message)
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                TopicPartitionOffset = new TopicPartitionOffset(AiKafkaTopics.Analysis, new Partition(1), new Offset(42)),
            });

        using var producer = CreateProducer(producerClient.Object);
        var analysisEvent = CreateEvent();

        var result = await producer.PublishAsync(analysisEvent);

        result.IsSuccess.Should().BeTrue();
        result.Topic.Should().Be(AiKafkaTopics.Analysis);
        result.Key.Should().Be("corr-1");
        published.Should().NotBeNull();
        published!.Key.Should().Be("corr-1");
        published.Value.Should().Contain("\"eventId\":\"evt-1\"");
        published.Value.Should().Contain("\"correlationId\":\"corr-1\"");
        published.Value.Should().Contain("\"payload\":\"{\\u0022orderId\\u0022:123}\"");
    }

    [Fact]
    public async Task PublishAsync_UsesEventIdAsKeyWhenCorrelationIdIsMissing()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        Message<string, string>? published = null;
        producerClient
            .Setup(client => client.ProduceAsync(AiKafkaTopics.Analysis, It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => published = message)
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                TopicPartitionOffset = new TopicPartitionOffset(AiKafkaTopics.Analysis, new Partition(0), new Offset(7)),
            });

        using var producer = CreateProducer(producerClient.Object);
        var analysisEvent = CreateEvent(correlationId: null);

        var result = await producer.PublishAsync(analysisEvent);

        result.IsSuccess.Should().BeTrue();
        result.Key.Should().Be("evt-1");
        published.Should().NotBeNull();
        published!.Key.Should().Be("evt-1");
    }

    [Fact]
    public async Task PublishAsync_ReturnsFailureResultWhenKafkaPublishFails()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        producerClient
            .Setup(client => client.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.Local_MsgTimedOut),
                new DeliveryResult<string, string>()));

        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent());

        result.IsSuccess.Should().BeFalse();
        result.Topic.Should().Be(AiKafkaTopics.Analysis);
        result.Key.Should().Be("corr-1");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    private static AiAnalysisProducer CreateProducer(IProducer<string, string> producerClient)
        => new(
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                AiAnalysisTopic = AiKafkaTopics.Analysis,
                ConsumerGroupId = "hookbridge-ai-tests",
            }),
            new TestLogger<AiAnalysisProducer>(),
            _ => producerClient);

    private static AiAnalysisEventDto CreateEvent(string? correlationId = "corr-1")
        => new()
        {
            EventId = "evt-1",
            CorrelationId = correlationId,
            Source = "hookbridge.worker",
            EventType = "webhook.delivery.failed",
            FailureReason = "HTTP 500",
            Payload = "{\"orderId\":123}",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:15:30Z"),
        };
}
