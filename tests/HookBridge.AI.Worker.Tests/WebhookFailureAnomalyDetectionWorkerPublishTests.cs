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
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = CreateWorker(request, producer.Object, acknowledgeSignal);

        await worker.StartAsync(CancellationToken.None);
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesWhenAnomalyDetected()
    {
        var request = Request(failedDeliveries: 10, rateLimitCount: 50, retryCount: 100);
        var producer = new Mock<IAiAnomalyProducer>();
        var publishSignal = new TaskCompletionSource<AiAnomalyEventDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer
            .Setup(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiAnomalyEventDto, CancellationToken>((dto, _) => publishSignal.TrySetResult(dto))
            .ReturnsAsync(AiKafkaPublishResult.Success(AiKafkaTopics.Anomalies, "corr-1", 0, 1, DateTime.UtcNow));
        var worker = CreateWorker(request, producer.Object, acknowledgeSignal);

        await worker.StartAsync(CancellationToken.None);
        var published = await publishSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Once);
        published.CorrelationId.Should().Be("corr-1");
        published.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike);
    }

    private static WebhookFailureAnomalyDetectionWorker CreateWorker(WebhookFailureAnomalyDetectionRequestDto request, IAiAnomalyProducer producer, TaskCompletionSource acknowledgeSignal)
    {
        var repository = new Mock<IWebhookFailureAnomalyDetectionRepository>();
        repository
            .Setup(client => client.InsertAsync(It.IsAny<WebhookFailureAnomalyDetectionResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new WebhookFailureAnomalyDetectionWorker(
            new SingleMessageConsumer(request, acknowledgeSignal),
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

    private static WebhookFailureAnomalyDetectionRequestDto Request(int failedDeliveries, int rateLimitCount, int retryCount = 0)
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
                RetryCount = retryCount,
                RateLimitCount = rateLimitCount
            },
            BaselineWindow = new WebhookFailureMetricWindowDto
            {
                WindowStartUtc = new DateTime(2026, 5, 14, 8, 0, 0, DateTimeKind.Utc),
                WindowEndUtc = new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc),
                TotalDeliveries = 100,
                SuccessfulDeliveries = 90,
                FailedDeliveries = 10,
                RetryCount = 1,
                RateLimitCount = 1
            }
        };

    private sealed class SingleMessageConsumer : IWebhookFailureAnomalyDetectionConsumer
    {
        private readonly WebhookFailureAnomalyDetectionRequestDto _request;
        private readonly TaskCompletionSource _acknowledgeSignal;

        public SingleMessageConsumer(WebhookFailureAnomalyDetectionRequestDto request, TaskCompletionSource acknowledgeSignal)
        {
            _request = request;
            _acknowledgeSignal = acknowledgeSignal;
        }

        public async IAsyncEnumerable<WebhookFailureAnomalyDetectionMessage> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return new WebhookFailureAnomalyDetectionMessage(_request, _ =>
            {
                _acknowledgeSignal.TrySetResult();
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
