using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class WebhookEventConsumerWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_LogsReceivedEvent()
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

        var logger = new TestLogger<HookBridge.Worker.WebhookEventConsumerWorker>();
        var worker = new TestWebhookEventConsumerWorker(
            kafkaConsumerMock.Object,
            Options.Create(new KafkaSettings
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                ConsumerGroupId = "hookbridge-worker",
            }),
            logger);

        using var cts = new CancellationTokenSource();
        await worker.RunOnceAsync(cts.Token);

        Assert.Contains(logger.Records, record =>
            record.Message.Contains("Webhook event received.") &&
            record.Message.Contains("tenant-1") &&
            record.Message.Contains("evt-1") &&
            record.Message.Contains("order.created") &&
            record.Message.Contains("corr-1"));
    }

    private static async IAsyncEnumerable<WebhookEventMessage> GetMessages(WebhookEventMessage message)
    {
        yield return message;
        await Task.CompletedTask;
    }

    private sealed class TestWebhookEventConsumerWorker : HookBridge.Worker.WebhookEventConsumerWorker
    {
        public TestWebhookEventConsumerWorker(
            IKafkaConsumer kafkaConsumer,
            IOptions<KafkaSettings> kafkaOptions,
            TestLogger<HookBridge.Worker.WebhookEventConsumerWorker> logger)
            : base(kafkaConsumer, kafkaOptions, logger)
        {
        }

        public Task RunOnceAsync(CancellationToken token)
        {
            return ExecuteAsync(token);
        }
    }
}
