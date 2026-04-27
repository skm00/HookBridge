using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class WebhookRetryConsumerWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ConsumesRetryMessage()
    {
        var message = CreateMessage(DateTime.UtcNow);
        var kafkaConsumerMock = CreateKafkaConsumer([message]);
        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        var logger = new TestLogger<HookBridge.Worker.WebhookRetryConsumerWorker>();
        var worker = new TestWebhookRetryConsumerWorker(kafkaConsumerMock.Object, deliveryServiceMock.Object, logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        deliveryServiceMock.Verify(x => x.ProcessRetryAsync(It.Is<WebhookRetryMessage>(m => m.EventId == "evt-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DelaysWhenNextRetryAtIsFuture()
    {
        var message = CreateMessage(DateTime.UtcNow.AddMilliseconds(500));
        var kafkaConsumerMock = CreateKafkaConsumer([message]);
        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        var logger = new TestLogger<HookBridge.Worker.WebhookRetryConsumerWorker>();
        var worker = new TestWebhookRetryConsumerWorker(kafkaConsumerMock.Object, deliveryServiceMock.Object, logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        Assert.Single(worker.Delays);
        Assert.True(worker.Delays[0] > TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDelayWhenNextRetryAtIsPast()
    {
        var message = CreateMessage(DateTime.UtcNow.AddMilliseconds(-500));
        var kafkaConsumerMock = CreateKafkaConsumer([message]);
        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        var logger = new TestLogger<HookBridge.Worker.WebhookRetryConsumerWorker>();
        var worker = new TestWebhookRetryConsumerWorker(kafkaConsumerMock.Object, deliveryServiceMock.Object, logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        Assert.Empty(worker.Delays);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterException()
    {
        var messages = new[]
        {
            CreateMessage(DateTime.UtcNow, eventId: "evt-1"),
            CreateMessage(DateTime.UtcNow, eventId: "evt-2"),
        };

        var kafkaConsumerMock = CreateKafkaConsumer(messages);
        var deliveryServiceMock = new Mock<IWebhookDeliveryService>();
        deliveryServiceMock
            .SetupSequence(x => x.ProcessRetryAsync(It.IsAny<WebhookRetryMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kaboom"))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<HookBridge.Worker.WebhookRetryConsumerWorker>();
        var worker = new TestWebhookRetryConsumerWorker(kafkaConsumerMock.Object, deliveryServiceMock.Object, logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        deliveryServiceMock.Verify(x => x.ProcessRetryAsync(It.IsAny<WebhookRetryMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Contains(logger.Records, r => r.Level == Microsoft.Extensions.Logging.LogLevel.Error && r.Message.Contains("Retry message processing failed"));
    }

    private static Mock<IKafkaConsumer> CreateKafkaConsumer(IEnumerable<WebhookRetryMessage> messages)
    {
        var kafkaConsumerMock = new Mock<IKafkaConsumer>();
        kafkaConsumerMock
            .Setup(c => c.ConsumeAsync<WebhookRetryMessage>("webhook-retry", "hookbridge-worker-retry", It.IsAny<CancellationToken>()))
            .Returns(GetMessages(messages));
        return kafkaConsumerMock;
    }

    private static WebhookRetryMessage CreateMessage(DateTime nextRetryAt, string eventId = "evt-1") => new()
    {
        TenantId = "tenant-1",
        EventId = eventId,
        SubscriptionId = "sub-1",
        AttemptNumber = 2,
        NextRetryAt = nextRetryAt,
        CorrelationId = "corr-1",
    };

    private static async IAsyncEnumerable<WebhookRetryMessage> GetMessages(IEnumerable<WebhookRetryMessage> messages)
    {
        foreach (var message in messages)
        {
            yield return message;
        }

        await Task.CompletedTask;
    }

    private sealed class TestWebhookRetryConsumerWorker(
        IKafkaConsumer kafkaConsumer,
        IWebhookDeliveryService webhookDeliveryService,
        TestLogger<HookBridge.Worker.WebhookRetryConsumerWorker> logger)
        : HookBridge.Worker.WebhookRetryConsumerWorker(kafkaConsumer, webhookDeliveryService, logger)
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task RunOnceAsync(CancellationToken token)
        {
            return ExecuteAsync(token);
        }

        protected override Task DelayUntilAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }
}
