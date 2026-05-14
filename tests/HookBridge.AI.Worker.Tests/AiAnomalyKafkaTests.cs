using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnomalyKafkaTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AiKafkaTopics_Anomalies_HasExpectedValue()
    {
        AiKafkaTopics.Anomalies.Should().Be("hookbridge.ai.anomalies");
        new AiKafkaOptions().AnomaliesTopic.Should().Be(AiKafkaTopics.Anomalies);
    }

    [Fact]
    public void AiAnomalyEventDto_SerializesAndDeserializesEnumsAsStrings()
    {
        var dto = CreateEvent();

        var json = JsonSerializer.Serialize(dto, SerializerOptions);
        var result = JsonSerializer.Deserialize<AiAnomalyEventDto>(json, SerializerOptions);

        json.Should().Contain("\"anomalyType\":\"RateLimitSpike\"");
        json.Should().Contain("\"riskLevel\":\"High\"");
        result.Should().NotBeNull();
        result!.AnomalyId.Should().Be("anm-1");
        result.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike);
        result.RiskLevel.Should().Be(AiRiskLevel.High);
    }

    [Fact]
    public void AiAnomalyEventDto_UnknownAnomalyType_DeserializesAsUnknown()
    {
        const string json = "{\"anomalyId\":\"anm-1\",\"customerId\":\"cust-1\",\"anomalyType\":\"NewSpike\",\"riskLevel\":\"High\",\"createdAtUtc\":\"2026-05-14T10:16:30Z\"}";

        var result = JsonSerializer.Deserialize<AiAnomalyEventDto>(json, SerializerOptions);

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be(AiAnomalyType.Unknown);
    }

    [Fact]
    public async Task Producer_UsesCorrelationIdAsKey()
    {
        var producerClient = CreateKafkaProducer(out var published, true);
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent());

        result.IsSuccess.Should().BeTrue();
        result.Topic.Should().Be(AiKafkaTopics.Anomalies);
        result.Key.Should().Be("corr-1");
        published().Should().NotBeNull();
        published()!.Key.Should().Be("corr-1");
    }

    [Fact]
    public async Task Producer_FallsBackToAnomalyIdAsKey()
    {
        var producerClient = CreateKafkaProducer(out var published, true);
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent(correlationId: null));

        result.IsSuccess.Should().BeTrue();
        result.Key.Should().Be("anm-1");
        published().Should().NotBeNull();
        published()!.Key.Should().Be("anm-1");
    }

    [Fact]
    public async Task Producer_ReturnsFailureResultWhenPublishFails()
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
        result.Topic.Should().Be(AiKafkaTopics.Anomalies);
        result.Key.Should().Be("corr-1");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.PublishedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Consumer_HandlesInvalidJsonSafelyAndContinues()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Anomalies));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("bad", "{not-json"))
            .Returns(BuildResult("corr-1", JsonSerializer.Serialize(CreateEvent(), SerializerOptions)))
            .Throws(new OperationCanceledException());
        var logger = new TestLogger<AiAnomalyConsumer>();
        var consumer = CreateConsumer(kafkaClient.Object, logger);

        AiAnomalyEventDto? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            result = message;
            break;
        }

        result.Should().NotBeNull();
        result!.AnomalyId.Should().Be("anm-1");
        logger.Records.Should().Contain(record =>
            record.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            record.Message.Contains("Invalid AI anomaly Kafka message skipped", StringComparison.Ordinal));
    }


    [Fact]
    public async Task Consumer_HandlesUnknownAnomalyTypeSafely()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Anomalies));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("corr-1", "{\"anomalyId\":\"anm-1\",\"customerId\":\"cust-1\",\"anomalyType\":\"FutureSpike\",\"riskLevel\":\"High\",\"createdAtUtc\":\"2026-05-14T10:16:30Z\"}"))
            .Throws(new OperationCanceledException());
        var consumer = CreateConsumer(kafkaClient.Object);

        AiAnomalyEventDto? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            result = message;
            break;
        }

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be(AiAnomalyType.Unknown);
    }

    private static Mock<IProducer<string, string>> CreateKafkaProducer(out Func<Message<string, string>?> published, bool succeeds)
    {
        Message<string, string>? message = null;
        published = () => message;
        var producerClient = new Mock<IProducer<string, string>>();
        if (succeeds)
        {
            producerClient
                .Setup(client => client.ProduceAsync(AiKafkaTopics.Anomalies, It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, string>, CancellationToken>((_, kafkaMessage, _) => message = kafkaMessage)
                .ReturnsAsync(new DeliveryResult<string, string>
                {
                    TopicPartitionOffset = new TopicPartitionOffset(AiKafkaTopics.Anomalies, new Partition(2), new Offset(99)),
                });
        }

        return producerClient;
    }

    private static AiAnomalyProducer CreateProducer(IProducer<string, string> producerClient)
        => new(
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests",
            }),
            new TestLogger<AiAnomalyProducer>(),
            _ => producerClient);

    private static AiAnomalyConsumer CreateConsumer(IConsumer<string, string> kafkaClient, TestLogger<AiAnomalyConsumer>? logger = null)
        => new(
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests",
                EnableAutoCommit = true,
            }),
            logger ?? new TestLogger<AiAnomalyConsumer>(),
            _ => kafkaClient);

    private static ConsumeResult<string, string> BuildResult(string key, string value)
        => new()
        {
            Topic = AiKafkaTopics.Anomalies,
            Partition = new Partition(0),
            Offset = new Offset(12),
            Message = new Message<string, string>
            {
                Key = key,
                Value = value,
            },
        };

    private static AiAnomalyEventDto CreateEvent(string? correlationId = "corr-1")
        => new()
        {
            AnomalyId = "anm-1",
            EventId = "evt-1",
            CorrelationId = correlationId,
            CustomerId = "cust-1",
            CustomerIdType = "MDM",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            EventType = "OrderCreated",
            AnomalyType = AiAnomalyType.RateLimitSpike,
            RiskLevel = AiRiskLevel.High,
            AnomalyScore = 78,
            Summary = "HTTP 429 rate-limit failures increased sharply compared to the baseline window.",
            Recommendation = "Reduce concurrency and retry with exponential backoff.",
            Source = "HookBridge.AI.Worker",
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 16, 30, DateTimeKind.Utc),
        };
}
