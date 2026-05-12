using Confluent.Kafka;
using FluentAssertions;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class KafkaProducerTests
{
    [Fact]
    public async Task ProduceAsync_ThrowsWhenTopicIsEmpty()
    {
        using var producer = CreateProducer();

        var act = () => producer.ProduceAsync("", "tenant-1", new { value = 1 });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Kafka topic cannot be empty.*");
    }

    [Fact]
    public async Task ProduceAsync_ThrowsWhenKeyIsEmpty()
    {
        using var producer = CreateProducer();

        var act = () => producer.ProduceAsync("webhook-events", "", new { value = 1 });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Kafka key cannot be empty.*");
    }

    [Fact]
    public async Task ProduceAsync_ThrowsWhenMessageIsNull()
    {
        using var producer = CreateProducer();

        var act = () => producer.ProduceAsync<object>("webhook-events", "tenant-1", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ProduceAsync_SerializesMessageAndPublishesToConfiguredTopic()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        Message<string, string>? published = null;
        producerClient
            .Setup(client => client.ProduceAsync("webhook-events", It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => published = message)
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                TopicPartitionOffset = new TopicPartitionOffset("webhook-events", new Partition(0), new Offset(12)),
            });

        using var producer = CreateProducer(producerClient.Object);

        await producer.ProduceAsync("webhook-events", "tenant-1", new TestPayload("evt-1", 3));

        published.Should().NotBeNull();
        published!.Key.Should().Be("tenant-1");
        published.Value.Should().Be("{\"EventId\":\"evt-1\",\"Attempt\":3}");
    }

    [Fact]
    public async Task ProduceAsync_RethrowsKafkaPublishFailures()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        producerClient
            .Setup(client => client.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.Local_MsgTimedOut),
                new DeliveryResult<string, string>()));

        using var producer = CreateProducer(producerClient.Object);

        var act = () => producer.ProduceAsync("webhook-events", "tenant-1", new TestPayload("evt-1", 1));

        await act.Should().ThrowAsync<ProduceException<string, string>>();
    }

    [Fact]
    public void Constructor_ConfiguresSaslSslProducerSettings()
    {
        ProducerConfig? capturedConfig = null;
        var producerClient = new Mock<IProducer<string, string>>();

        using var producer = CreateProducer(
            producerClient.Object,
            new KafkaSettings
            {
                BootstrapServers = "broker:9093",
                MessageTimeoutMs = 5000,
                SecurityProtocol = "SaslSsl",
                SaslMechanism = "Plain",
                SaslUsername = "user",
                SaslPassword = "pass",
            },
            config => capturedConfig = config);

        capturedConfig.Should().NotBeNull();
        capturedConfig!.BootstrapServers.Should().Be("broker:9093");
        capturedConfig.SecurityProtocol.Should().Be(SecurityProtocol.SaslSsl);
        capturedConfig.SaslMechanism.Should().Be(SaslMechanism.Plain);
        capturedConfig.SaslUsername.Should().Be("user");
        capturedConfig.SaslPassword.Should().Be("pass");
    }

    [Fact]
    public void Dispose_FlushesAndDisposesProducerOnlyOnce()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        var producer = CreateProducer(producerClient.Object);

        producer.Dispose();
        producer.Dispose();

        producerClient.Verify(client => client.Flush(It.IsAny<TimeSpan>()), Times.Once);
        producerClient.Verify(client => client.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ProduceAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var producer = CreateProducer();
        producer.Dispose();

        var act = () => producer.ProduceAsync("webhook-events", "tenant-1", new TestPayload("evt-1", 1));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static KafkaProducer CreateProducer(
        IProducer<string, string>? producerClient = null,
        KafkaSettings? settings = null,
        Action<ProducerConfig>? captureConfig = null)
    {
        producerClient ??= Mock.Of<IProducer<string, string>>();
        settings ??= new KafkaSettings
        {
            BootstrapServers = "localhost:9092",
            MessageTimeoutMs = 10000,
            SecurityProtocol = "Plaintext",
            SaslMechanism = string.Empty,
            SaslUsername = null,
            SaslPassword = null,
        };

        return new KafkaProducer(
            Options.Create(settings),
            NullLogger<KafkaProducer>.Instance,
            config =>
            {
                captureConfig?.Invoke(config);
                return producerClient;
            });
    }

    private sealed record TestPayload(string EventId, int Attempt);
}
