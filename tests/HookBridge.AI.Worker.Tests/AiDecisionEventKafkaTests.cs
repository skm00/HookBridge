using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mappers;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDecisionEventKafkaTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void AiKafkaTopics_Decisions_HasExpectedValue()
    {
        AiKafkaTopics.Decisions.Should().Be("hookbridge.ai.decisions");
        new AiKafkaOptions().AiDecisionsTopic.Should().Be(AiKafkaTopics.Decisions);
    }

    [Fact]
    public void AiDecisionEventDto_SerializesAndDeserializesEnumsAsStrings()
    {
        var dto = CreateEvent();

        var json = JsonSerializer.Serialize(dto, SerializerOptions);
        var result = JsonSerializer.Deserialize<AiDecisionEventDto>(json, SerializerOptions);

        json.Should().Contain("\"decisionType\":\"RetryDecision\"");
        result.Should().NotBeNull();
        result!.DecisionId.Should().Be("dec-1");
        result.DecisionType.Should().Be(AiDecisionEventType.RetryDecision);
    }

    [Fact]
    public void AiDecisionEventDto_UnknownDecisionType_DeserializesAsUnknown()
    {
        const string json = "{\"decisionId\":\"dec-1\",\"decisionType\":\"FutureDecision\",\"createdAtUtc\":\"2026-05-14T10:31:00Z\"}";

        var result = JsonSerializer.Deserialize<AiDecisionEventDto>(json, SerializerOptions);

        result.Should().NotBeNull();
        result!.DecisionType.Should().Be(AiDecisionEventType.Unknown);
    }

    [Fact]
    public async Task Producer_UsesCorrelationIdAsKey()
    {
        var producerClient = CreateKafkaProducer(out var published);
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent());

        result.IsSuccess.Should().BeTrue();
        result.Topic.Should().Be(AiKafkaTopics.Decisions);
        result.Key.Should().Be("corr-1");
        published()!.Key.Should().Be("corr-1");
    }

    [Fact]
    public async Task Producer_FallsBackToEventIdAsKey()
    {
        var producerClient = CreateKafkaProducer(out var published);
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent(correlationId: null));

        result.IsSuccess.Should().BeTrue();
        result.Key.Should().Be("evt-1");
        published()!.Key.Should().Be("evt-1");
    }

    [Fact]
    public async Task Producer_FallsBackToDecisionIdAsKey()
    {
        var producerClient = CreateKafkaProducer(out var published);
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent(correlationId: null, eventId: null));

        result.IsSuccess.Should().BeTrue();
        result.Key.Should().Be("dec-1");
        published()!.Key.Should().Be("dec-1");
    }

    [Fact]
    public async Task Producer_ReturnsFailureResultWhenPublishFails()
    {
        var producerClient = new Mock<IProducer<string, string>>();
        producerClient
            .Setup(client => client.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(new Error(ErrorCode.Local_MsgTimedOut), new DeliveryResult<string, string>()));
        using var producer = CreateProducer(producerClient.Object);

        var result = await producer.PublishAsync(CreateEvent());

        result.IsSuccess.Should().BeFalse();
        result.Topic.Should().Be(AiKafkaTopics.Decisions);
        result.Key.Should().Be("corr-1");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Consumer_HandlesInvalidJsonSafelyAndContinues()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Decisions));
        kafkaClient.SetupSequence(client => client.Consume(It.IsAny<CancellationToken>()))
            .Returns(BuildResult("bad", "{not-json"))
            .Returns(BuildResult("corr-1", JsonSerializer.Serialize(CreateEvent(), SerializerOptions)))
            .Throws(new OperationCanceledException());
        var logger = new TestLogger<AiDecisionEventConsumer>();
        var consumer = CreateConsumer(kafkaClient.Object, logger);

        AiDecisionEventDto? result = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            result = message;
            break;
        }

        result.Should().NotBeNull();
        logger.Records.Should().Contain(record => record.Message.Contains("Invalid decision event skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Consumer_RespectsCancellationTokenBeforeConsuming()
    {
        var kafkaClient = new Mock<IConsumer<string, string>>();
        kafkaClient.Setup(client => client.Subscribe(AiKafkaTopics.Decisions));
        var consumer = CreateConsumer(kafkaClient.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var messages = new List<AiDecisionEventDto>();
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(message);
        }

        messages.Should().BeEmpty();
        kafkaClient.Verify(client => client.Consume(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Mapper_FromAuditRecord_PreservesDecisionMetadata()
    {
        var record = new AiDecisionAuditRecord
        {
            DecisionId = "dec-1",
            AuditId = "aud-1",
            EventId = "evt-1",
            CorrelationId = "corr-1",
            DecisionType = AiDecisionAuditType.SafeModeEvaluation,
            ConfidenceScore = 0.82,
            ConfidenceLevel = "High",
            PromptName = "WebhookFailureAnalysis",
            PromptVersion = "v1.0.0",
            PromptHash = "sha256:abc",
            UsedFallback = true,
            FallbackReason = "AiDisabled",
            SafeModeDecision = "Allowed",
            IsActionAllowed = false,
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc)
        };

        var dto = AiDecisionEventMapper.FromAuditRecord(record);

        dto.DecisionId.Should().Be("dec-1");
        dto.EventId.Should().Be("evt-1");
        dto.CorrelationId.Should().Be("corr-1");
        dto.DecisionType.Should().Be(AiDecisionEventType.SafeModeEvaluation);
        dto.ConfidenceScore.Should().Be(0.82);
        dto.PromptVersion.Should().Be("v1.0.0");
        dto.UsedFallback.Should().BeTrue();
        dto.SafeModeDecision.Should().Be("Allowed");
    }

    private static Mock<IProducer<string, string>> CreateKafkaProducer(out Func<Message<string, string>?> published)
    {
        Message<string, string>? message = null;
        published = () => message;
        var producerClient = new Mock<IProducer<string, string>>();
        producerClient
            .Setup(client => client.ProduceAsync(AiKafkaTopics.Decisions, It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, kafkaMessage, _) => message = kafkaMessage)
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                TopicPartitionOffset = new TopicPartitionOffset(AiKafkaTopics.Decisions, new Partition(2), new Offset(99)),
            });

        return producerClient;
    }

    private static AiDecisionEventProducer CreateProducer(IProducer<string, string> producerClient)
        => new(Options.Create(new AiKafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = "Plaintext", AiDecisionsTopic = AiKafkaTopics.Decisions, ConsumerGroupId = "hookbridge-ai-tests" }), new TestLogger<AiDecisionEventProducer>(), _ => producerClient);

    private static AiDecisionEventConsumer CreateConsumer(IConsumer<string, string> kafkaClient, TestLogger<AiDecisionEventConsumer>? logger = null)
        => new(Options.Create(new AiKafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = "Plaintext", AiDecisionsTopic = AiKafkaTopics.Decisions, ConsumerGroupId = "hookbridge-ai-tests", EnableAutoCommit = true }), logger ?? new TestLogger<AiDecisionEventConsumer>(), _ => kafkaClient);

    private static ConsumeResult<string, string> BuildResult(string key, string value)
        => new() { Topic = AiKafkaTopics.Decisions, Partition = new Partition(0), Offset = new Offset(12), Message = new Message<string, string> { Key = key, Value = value } };

    private static AiDecisionEventDto CreateEvent(string? correlationId = "corr-1", string? eventId = "evt-1")
        => new()
        {
            DecisionId = "dec-1",
            AuditId = "aud-1",
            EventId = eventId,
            CorrelationId = correlationId,
            CustomerId = "cust-1",
            AgentName = "RetryAgent",
            DecisionType = AiDecisionEventType.RetryDecision,
            Decision = "RetryWithBackoff",
            ConfidenceScore = 0.82,
            ConfidenceLevel = "High",
            Source = "HookBridge.AI.Worker",
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc)
        };
}
