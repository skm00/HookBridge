using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Worker;

public class WebhookEventConsumerWorker : BackgroundService
{
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookEventConsumerWorker> _logger;
    private readonly KafkaSettings _kafkaSettings;
    private readonly WorkerTransactionRunner _transactionRunner;

    public WebhookEventConsumerWorker(
        IKafkaConsumer kafkaConsumer,
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<WebhookEventConsumerWorker> logger,
        WorkerTransactionRunner transactionRunner)
    {
        _kafkaConsumer = kafkaConsumer;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _kafkaSettings = kafkaOptions.Value;
        _transactionRunner = transactionRunner;
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
                await using var scope = _scopeFactory.CreateAsyncScope();
                var webhookDeliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

                await _transactionRunner.RunAsync(
                    "Process webhook event",
                    transaction =>
                    {
                        transaction.SetLabel("tenantId", message.TenantId);
                        transaction.SetLabel("eventId", message.EventId);
                        transaction.SetLabel("eventType", message.EventType);
                        transaction.SetLabel("correlationId", message.CorrelationId);
                    },
                    token => webhookDeliveryService.ProcessEventAsync(message, token),
                    stoppingToken);

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
