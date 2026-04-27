using HookBridge.Application.Interfaces.Services;
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
    private readonly IWebhookDeliveryService _webhookDeliveryService;
    private readonly ILogger<WebhookEventConsumerWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;

    public WebhookEventConsumerWorker(
        IKafkaConsumer kafkaConsumer,
        IWebhookDeliveryService webhookDeliveryService,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<WebhookEventConsumerWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer;
        _webhookDeliveryService = webhookDeliveryService;
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

            try
            {
                await _webhookDeliveryService.ProcessEventAsync(message, stoppingToken);

                _logger.LogInformation(
                    "Webhook event processed successfully. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, CorrelationId: {CorrelationId}",
                    message.TenantId,
                    message.EventId,
                    message.EventType,
                    message.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Webhook event processing failed. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, CorrelationId: {CorrelationId}",
                    message.TenantId,
                    message.EventId,
                    message.EventType,
                    message.CorrelationId);
            }
        }
    }
}
