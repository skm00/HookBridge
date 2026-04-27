using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class WebhookEventConsumerWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_CallsWebhookDeliveryService()
    {
        var message = new WebhookEventMessage
        {
            TenantId = "tenant-1",
            EventId = "evt-1",
            EventType = "order.created",
            CorrelationId = "corr-1",
            ReceivedAt = DateTime.UtcNow,
        };

        var kafkaConsumerMock = new Mock<IKafkaConsumer>();
        kafkaConsumerMock
            .Setup(c => c.ConsumeAsync<WebhookEventMessage>("webhook-events", "hookbridge-worker", It.IsAny<CancellationToken>()))
            .Returns(GetMessages(message));

        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        var logger = new TestLogger<HookBridge.Worker.WebhookEventConsumerWorker>();
        var worker = new TestWebhookEventConsumerWorker(
            kafkaConsumerMock.Object,
            deliveryServiceMock.Object,
            Options.Create(new KafkaSettings
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                ConsumerGroupId = "hookbridge-worker",
            }),
            logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        deliveryServiceMock.Verify(x => x.ProcessEventAsync(It.Is<WebhookEventMessage>(m => m.EventId == "evt-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterServiceException()
    {
        var messages = new[]
        {
            new WebhookEventMessage { TenantId = "tenant-1", EventId = "evt-1", EventType = "order.created", CorrelationId = "corr-1", ReceivedAt = DateTime.UtcNow },
            new WebhookEventMessage { TenantId = "tenant-1", EventId = "evt-2", EventType = "order.created", CorrelationId = "corr-2", ReceivedAt = DateTime.UtcNow },
        };

        var kafkaConsumerMock = new Mock<IKafkaConsumer>();
        kafkaConsumerMock
            .Setup(c => c.ConsumeAsync<WebhookEventMessage>("webhook-events", "hookbridge-worker", It.IsAny<CancellationToken>()))
            .Returns(GetMessages(messages));

        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        deliveryServiceMock
            .SetupSequence(x => x.ProcessEventAsync(It.IsAny<WebhookEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kaboom"))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<HookBridge.Worker.WebhookEventConsumerWorker>();
        var worker = new TestWebhookEventConsumerWorker(
            kafkaConsumerMock.Object,
            deliveryServiceMock.Object,
            Options.Create(new KafkaSettings
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                ConsumerGroupId = "hookbridge-worker",
            }),
            logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        deliveryServiceMock.Verify(x => x.ProcessEventAsync(It.IsAny<WebhookEventMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Contains(logger.Records, r => r.Level == Microsoft.Extensions.Logging.LogLevel.Error && r.Message.Contains("Webhook event processing failed"));
        Assert.Contains(logger.Records, r => r.Message.Contains("evt-2") && r.Message.Contains("processed successfully"));
    }

    private static async IAsyncEnumerable<WebhookEventMessage> GetMessages(WebhookEventMessage message)
    {
        yield return message;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<WebhookEventMessage> GetMessages(IEnumerable<WebhookEventMessage> messages)
    {
        foreach (var message in messages)
        {
            yield return message;
        }

        await Task.CompletedTask;
    }

    private sealed class TestWebhookEventConsumerWorker : HookBridge.Worker.WebhookEventConsumerWorker
    {
        public TestWebhookEventConsumerWorker(
            IKafkaConsumer kafkaConsumer,
            IWebhookDeliveryService webhookDeliveryService,
            IOptions<KafkaSettings> kafkaOptions,
            TestLogger<HookBridge.Worker.WebhookEventConsumerWorker> logger)
            : base(kafkaConsumer, webhookDeliveryService, kafkaOptions, logger)
        {
        }

        public Task RunOnceAsync(CancellationToken token)
        {
            return ExecuteAsync(token);
        }
    }
}
