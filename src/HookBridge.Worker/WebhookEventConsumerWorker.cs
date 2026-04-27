using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Worker;

public class WebhookEventConsumerWorker : BackgroundService
{
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly ILogger<WebhookEventConsumerWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;

    public WebhookEventConsumerWorker(
        IKafkaConsumer kafkaConsumer,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<WebhookEventConsumerWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _logger = logger;
        _kafkaSettings = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _kafkaConsumer.ConsumeAsync<WebhookEventMessage>(
                           KafkaTopics.WebhookEvents,
                           _kafkaSettings.ConsumerGroupId,
                           stoppingToken))
        {
            _logger.LogInformation(
                "Webhook event received. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.EventType,
                message.CorrelationId);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
