using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HookBridge.Worker;

public class WebhookRetryConsumerWorker(
    IKafkaConsumer kafkaConsumer,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookRetryConsumerWorker> logger,
    WorkerTransactionRunner transactionRunner) : BackgroundService
{
    private const string RetryConsumerGroupId = "hookbridge-worker-retry";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in kafkaConsumer.ConsumeAsync<WebhookRetryMessage>(
                           KafkaTopics.WebhookRetry,
                           RetryConsumerGroupId,
                           stoppingToken))
        {
            logger.LogInformation(
                "Retry message consumed. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.SubscriptionId,
                message.AttemptNumber,
                message.NextRetryAt,
                message.CorrelationId);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var now = DateTime.UtcNow;
                if (message.NextRetryAt > now)
                {
                    var delay = message.NextRetryAt - now;
                    logger.LogInformation(
                        "Retry delayed until NextRetryAt. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                        message.TenantId,
                        message.EventId,
                        message.SubscriptionId,
                        message.AttemptNumber,
                        message.NextRetryAt,
                        message.CorrelationId);
                    await DelayUntilAsync(delay, stoppingToken);
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var webhookDeliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

                await transactionRunner.RunAsync(
                    "Process webhook retry",
                    transaction =>
                    {
                        transaction.SetLabel("tenantId", message.TenantId);
                        transaction.SetLabel("eventId", message.EventId);
                        transaction.SetLabel("subscriptionId", message.SubscriptionId);
                        transaction.SetLabel("attemptNumber", message.AttemptNumber);
                        transaction.SetLabel("correlationId", message.CorrelationId);
                    },
                    token => webhookDeliveryService.ProcessRetryAsync(message, token),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Retry message processing failed. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                    message.TenantId,
                    message.EventId,
                    message.SubscriptionId,
                    message.AttemptNumber,
                    message.NextRetryAt,
                    message.CorrelationId);
            }
        }
    }

    protected virtual Task DelayUntilAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
