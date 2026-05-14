using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookFailureAnomalyDetectionWorkerPublishTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNotPublishWhenNoAnomalyDetected()
    {
        var request = Request(failedDeliveries: 10, rateLimitCount: 0);
        var producer = new Mock<IAiAnomalyProducer>();
        var worker = CreateWorker(request, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesWhenAnomalyDetected()
    {
        var request = Request(failedDeliveries: 80, rateLimitCount: 50);
        var producer = new Mock<IAiAnomalyProducer>();
        AiAnomalyEventDto? published = null;
        producer
            .Setup(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiAnomalyEventDto, CancellationToken>((dto, _) => published = dto)
            .ReturnsAsync(AiKafkaPublishResult.Success(AiKafkaTopics.Anomalies, "corr-1", 0, 1, DateTime.UtcNow));
        var worker = CreateWorker(request, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Once);
        published.Should().NotBeNull();
        published!.CorrelationId.Should().Be("corr-1");
        published.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike);
    }

    private static WebhookFailureAnomalyDetectionWorker CreateWorker(WebhookFailureAnomalyDetectionRequestDto request, IAiAnomalyProducer producer)
    {
        var repository = new Mock<IWebhookFailureAnomalyDetectionRepository>();
        repository
            .Setup(client => client.InsertAsync(It.IsAny<WebhookFailureAnomalyDetectionResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new WebhookFailureAnomalyDetectionWorker(
            new SingleMessageConsumer(request),
            new WebhookFailureAnomalyDetectionService(),
            repository.Object,
            producer,
            new TestLogger<WebhookFailureAnomalyDetectionWorker>(),
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                WebhookFailureAnomalyDetectionTopic = AiKafkaTopics.FailureAnomalies,
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests"
            }));
    }

    private static WebhookFailureAnomalyDetectionRequestDto Request(int failedDeliveries, int rateLimitCount)
        => new()
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            TargetUrl = "https://customer.example.com/webhook",
            Environment = "qa",
            EventType = "OrderCreated",
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
            CurrentWindow = new WebhookFailureMetricWindowDto
            {
                WindowStartUtc = new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc),
                WindowEndUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
                TotalDeliveries = 100,
                SuccessfulDeliveries = 100 - failedDeliveries,
                FailedDeliveries = failedDeliveries,
                RateLimitCount = rateLimitCount
            },
            BaselineWindow = new WebhookFailureMetricWindowDto
            {
                WindowStartUtc = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc),
                WindowEndUtc = new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc),
                TotalDeliveries = 100,
                SuccessfulDeliveries = 90,
                FailedDeliveries = 10,
                RateLimitCount = 1
            }
        };

    private sealed class SingleMessageConsumer : IWebhookFailureAnomalyDetectionConsumer
    {
        private readonly WebhookFailureAnomalyDetectionRequestDto _request;

        public SingleMessageConsumer(WebhookFailureAnomalyDetectionRequestDto request) => _request = request;

        public async IAsyncEnumerable<WebhookFailureAnomalyDetectionMessage> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return new WebhookFailureAnomalyDetectionMessage(_request, _ => Task.CompletedTask);
            await Task.Yield();
        }
    }
}
