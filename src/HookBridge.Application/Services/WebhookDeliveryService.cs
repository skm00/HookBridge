using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Application.Models.Delivery;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class WebhookDeliveryService(
    IMongoRepository<IncomingEvent> incomingEventRepository,
    IMongoRepository<Subscription> subscriptionRepository,
    IMongoRepository<DeliveryAttempt> deliveryAttemptRepository,
    IDateTimeProvider dateTimeProvider,
    IWebhookDeliveryClient webhookDeliveryClient,
    IKafkaProducer kafkaProducer,
    IRetryPolicyService retryPolicyService,
    ILogger<WebhookDeliveryService> logger) : IWebhookDeliveryService
{
    public async Task ProcessEventAsync(WebhookEventMessage message, CancellationToken cancellationToken = default)
    {
        var incomingEvent = await incomingEventRepository.FirstOrDefaultAsync(
            x => x.TenantId == message.TenantId && x.EventId == message.EventId,
            cancellationToken);

        if (incomingEvent is null)
        {
            logger.LogWarning(
                "Incoming event not found. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.EventType,
                message.CorrelationId);
            return;
        }

        var subscriptions = await subscriptionRepository.FindAsync(
            x => x.TenantId == message.TenantId
                && x.EventType == message.EventType
                && x.IsActive,
            cancellationToken);

        if (subscriptions.Count == 0)
        {
            incomingEvent.Status = "NoSubscriptions";
            incomingEvent.UpdatedAt = dateTimeProvider.UtcNow;
            await incomingEventRepository.UpdateAsync(incomingEvent, cancellationToken);

            logger.LogInformation(
                "No active subscriptions matched event. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, CorrelationId: {CorrelationId}, DeliveryStatus: {DeliveryStatus}",
                message.TenantId,
                message.EventId,
                message.EventType,
                message.CorrelationId,
                incomingEvent.Status);
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var total = subscriptions.Count;
        var succeeded = 0;

        foreach (var subscription in subscriptions)
        {
            var request = BuildRequest(subscription, incomingEvent, message.CorrelationId);
            var result = await webhookDeliveryClient.SendAsync(request, cancellationToken);
            const int currentAttemptNumber = 1;

            if (result.IsSuccess)
            {
                succeeded++;
            }

            var attempt = new DeliveryAttempt
            {
                TenantId = incomingEvent.TenantId,
                EventId = incomingEvent.EventId,
                SubscriptionId = subscription.Id,
                EventType = incomingEvent.EventType,
                TargetUrl = subscription.TargetUrl,
                AttemptNumber = currentAttemptNumber,
                Status = result.IsSuccess ? DeliveryStatus.Success : DeliveryStatus.Failed,
                HttpStatusCode = result.HttpStatusCode,
                ResponseBody = result.ResponseBody,
                ErrorMessage = result.ErrorMessage,
                DurationMs = result.DurationMs,
                AttemptedAt = now,
                CorrelationId = message.CorrelationId,
                CreatedAt = now,
                UpdatedAt = null,
            };

            await deliveryAttemptRepository.AddAsync(attempt, cancellationToken);

            if (!result.IsSuccess)
            {
                var retryPolicy = new RetryPolicyDto
                {
                    MaxAttempts = subscription.RetryPolicy.MaxAttempts,
                    InitialDelaySeconds = subscription.RetryPolicy.InitialDelaySeconds,
                    BackoffType = subscription.RetryPolicy.BackoffType,
                };

                if (retryPolicyService.ShouldRetry(retryPolicy, currentAttemptNumber))
                {
                    var retryDelay = retryPolicyService.CalculateDelay(retryPolicy, currentAttemptNumber);
                    var nextRetryAt = dateTimeProvider.UtcNow.Add(retryDelay);
                    var nextAttemptNumber = currentAttemptNumber + 1;
                    var retryMessage = new WebhookRetryMessage
                    {
                        EventId = incomingEvent.EventId,
                        TenantId = incomingEvent.TenantId,
                        SubscriptionId = subscription.Id,
                        AttemptNumber = nextAttemptNumber,
                        NextRetryAt = nextRetryAt,
                        CorrelationId = message.CorrelationId,
                    };

                    try
                    {
                        await kafkaProducer.ProduceAsync(
                            KafkaTopics.WebhookRetry,
                            incomingEvent.TenantId,
                            retryMessage,
                            cancellationToken);

                        logger.LogInformation(
                            "Webhook retry scheduled. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, DelaySeconds: {DelaySeconds}, CorrelationId: {CorrelationId}",
                            incomingEvent.TenantId,
                            incomingEvent.EventId,
                            subscription.Id,
                            nextAttemptNumber,
                            nextRetryAt,
                            retryDelay.TotalSeconds,
                            message.CorrelationId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Failed to publish webhook retry message. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, CorrelationId: {CorrelationId}",
                            incomingEvent.TenantId,
                            incomingEvent.EventId,
                            subscription.Id,
                            nextAttemptNumber,
                            message.CorrelationId);
                    }
                }
            }

            logger.LogInformation(
                "Webhook delivery attempt completed. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, SubscriptionId: {SubscriptionId}, TargetUrl: {TargetUrl}, DeliveryStatus: {DeliveryStatus}, HttpStatusCode: {HttpStatusCode}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                attempt.TenantId,
                attempt.EventId,
                attempt.EventType,
                attempt.SubscriptionId,
                attempt.TargetUrl,
                attempt.Status,
                attempt.HttpStatusCode,
                attempt.DurationMs,
                attempt.CorrelationId);
        }

        incomingEvent.Status = succeeded switch
        {
            0 => "Failed",
            _ when succeeded == total => "Delivered",
            _ => "PartiallyFailed",
        };

        incomingEvent.UpdatedAt = dateTimeProvider.UtcNow;
        await incomingEventRepository.UpdateAsync(incomingEvent, cancellationToken);

        logger.LogInformation(
            "Incoming event delivery processing completed. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, DeliveryStatus: {DeliveryStatus}, CorrelationId: {CorrelationId}",
            incomingEvent.TenantId,
            incomingEvent.EventId,
            incomingEvent.EventType,
            incomingEvent.Status,
            message.CorrelationId);
    }

    private static WebhookDeliveryRequest BuildRequest(Subscription subscription, IncomingEvent incomingEvent, string? correlationId)
    {
        return new WebhookDeliveryRequest
        {
            TargetUrl = subscription.TargetUrl,
            EventId = incomingEvent.EventId,
            TenantId = incomingEvent.TenantId,
            EventType = incomingEvent.EventType,
            Payload = incomingEvent.Payload,
            Headers = subscription.Headers.Select(x => new KeyValueDto
            {
                Name = x.Name,
                Value = x.Value,
            }).ToList(),
            Authentication = subscription.Authentication is null ? null : new AuthenticationDto
            {
                Type = subscription.Authentication.Type,
                Basic = subscription.Authentication.Basic is null ? null : new BasicAuthDto
                {
                    Username = subscription.Authentication.Basic.Username,
                    Password = subscription.Authentication.Basic.Password,
                },
                OAuth2 = subscription.Authentication.OAuth2 is null ? null : new OAuth2ClientCredentialsDto
                {
                    TokenUrl = subscription.Authentication.OAuth2.TokenUrl,
                    ClientId = subscription.Authentication.OAuth2.ClientId,
                    ClientSecret = subscription.Authentication.OAuth2.ClientSecret,
                    Scope = subscription.Authentication.OAuth2.Scope,
                },
                ApiKeyHeader = subscription.Authentication.ApiKeyHeader is null ? null : new ApiKeyHeaderDto
                {
                    HeaderName = subscription.Authentication.ApiKeyHeader.HeaderName,
                    HeaderValue = subscription.Authentication.ApiKeyHeader.HeaderValue,
                },
                HmacSignature = subscription.Authentication.HmacSignature is null ? null : new HmacSignatureDto
                {
                    Secret = subscription.Authentication.HmacSignature.Secret,
                    HeaderName = subscription.Authentication.HmacSignature.HeaderName,
                    Algorithm = subscription.Authentication.HmacSignature.Algorithm,
                },
            },
            TimeoutSeconds = subscription.TimeoutSeconds,
            CorrelationId = correlationId,
        };
    }
}
