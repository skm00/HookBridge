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
    IFailedEventService failedEventService,
    IUsageService usageService,
    ISecretEncryptionService secretEncryptionService,
    ILogger<WebhookDeliveryService> logger,
    ITracingService? tracingService = null) : IWebhookDeliveryService
{
    public async Task ProcessEventAsync(WebhookEventMessage message, CancellationToken cancellationToken = default)
    {
        var incomingEvent = await (tracingService?.CaptureSpanAsync(
            "Load incoming event",
            "db.mongodb",
            () => incomingEventRepository.FirstOrDefaultAsync(
                x => x.TenantId == message.TenantId && x.EventId == message.EventId,
                cancellationToken))
            ?? incomingEventRepository.FirstOrDefaultAsync(
                x => x.TenantId == message.TenantId && x.EventId == message.EventId,
                cancellationToken));

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

        var subscriptions = await (tracingService?.CaptureSpanAsync(
            "Find subscriptions",
            "db.mongodb",
            () => subscriptionRepository.FindAsync(
                x => x.TenantId == message.TenantId
                    && (x.EventType == message.EventType || x.EventType == "*")
                    && x.IsActive,
                cancellationToken))
            ?? subscriptionRepository.FindAsync(
                x => x.TenantId == message.TenantId
                    && (x.EventType == message.EventType || x.EventType == "*")
                    && x.IsActive,
                cancellationToken));

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
            var request = BuildRequest(subscription, incomingEvent, message.CorrelationId, secretEncryptionService);
            var result = await (tracingService?.CaptureSpanAsync(
                "Send webhook",
                "external.http",
                () => webhookDeliveryClient.SendAsync(request, cancellationToken))
                ?? webhookDeliveryClient.SendAsync(request, cancellationToken));
            const int currentAttemptNumber = 1;
            if (result.IsSuccess)
            {
                succeeded++;
                await usageService.IncrementEventsDeliveredAsync(incomingEvent.TenantId, cancellationToken);
            }

            var attempt = CreateDeliveryAttempt(incomingEvent, subscription, result, currentAttemptNumber, message.CorrelationId, now);
            await (tracingService?.CaptureSpanAsync(
                "Store delivery attempt",
                "db.mongodb",
                () => deliveryAttemptRepository.AddAsync(attempt, cancellationToken))
                ?? deliveryAttemptRepository.AddAsync(attempt, cancellationToken));

            if (!result.IsSuccess)
            {
                await TryScheduleRetryAsync(incomingEvent, subscription, result, currentAttemptNumber, message.CorrelationId, cancellationToken);
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

    public async Task ProcessRetryAsync(WebhookRetryMessage message, CancellationToken cancellationToken = default)
    {
        var incomingEvent = await (tracingService?.CaptureSpanAsync(
            "Load incoming event",
            "db.mongodb",
            () => incomingEventRepository.FirstOrDefaultAsync(
                x => x.TenantId == message.TenantId && x.EventId == message.EventId,
                cancellationToken))
            ?? incomingEventRepository.FirstOrDefaultAsync(
                x => x.TenantId == message.TenantId && x.EventId == message.EventId,
                cancellationToken));

        if (incomingEvent is null)
        {
            logger.LogWarning(
                "Retry skipped because incoming event was not found. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.SubscriptionId,
                message.AttemptNumber,
                message.NextRetryAt,
                message.CorrelationId);
            return;
        }

        var subscription = await (tracingService?.CaptureSpanAsync(
            "Find subscriptions",
            "db.mongodb",
            () => subscriptionRepository.GetByIdAsync(message.SubscriptionId, cancellationToken))
            ?? subscriptionRepository.GetByIdAsync(message.SubscriptionId, cancellationToken));
        if (subscription is null)
        {
            logger.LogWarning(
                "Retry skipped because subscription was not found. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.SubscriptionId,
                message.AttemptNumber,
                message.NextRetryAt,
                message.CorrelationId);
            return;
        }

        if (!subscription.IsActive)
        {
            logger.LogInformation(
                "Retry skipped because subscription is inactive. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.SubscriptionId,
                message.AttemptNumber,
                message.NextRetryAt,
                message.CorrelationId);
            return;
        }

        var request = BuildRequest(subscription, incomingEvent, message.CorrelationId, secretEncryptionService);
        var result = await (tracingService?.CaptureSpanAsync(
            "Send webhook",
            "external.http",
            () => webhookDeliveryClient.SendAsync(request, cancellationToken))
            ?? webhookDeliveryClient.SendAsync(request, cancellationToken));
        var now = dateTimeProvider.UtcNow;
        var attempt = CreateDeliveryAttempt(incomingEvent, subscription, result, message.AttemptNumber, message.CorrelationId, now);
        await (tracingService?.CaptureSpanAsync(
            "Store delivery attempt",
            "db.mongodb",
            () => deliveryAttemptRepository.AddAsync(attempt, cancellationToken))
            ?? deliveryAttemptRepository.AddAsync(attempt, cancellationToken));

        if (result.IsSuccess)
        {
            await usageService.IncrementEventsDeliveredAsync(incomingEvent.TenantId, cancellationToken);
            logger.LogInformation(
                "Retry delivery succeeded. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
                message.TenantId,
                message.EventId,
                message.SubscriptionId,
                message.AttemptNumber,
                message.NextRetryAt,
                message.CorrelationId);
            return;
        }

        logger.LogInformation(
            "Retry delivery failed. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, CorrelationId: {CorrelationId}",
            message.TenantId,
            message.EventId,
            message.SubscriptionId,
            message.AttemptNumber,
            message.NextRetryAt,
            message.CorrelationId);
        await TryScheduleRetryAsync(incomingEvent, subscription, result, message.AttemptNumber, message.CorrelationId, cancellationToken);
    }

    private DeliveryAttempt CreateDeliveryAttempt(
        IncomingEvent incomingEvent,
        Subscription subscription,
        WebhookDeliveryResult result,
        int attemptNumber,
        string? correlationId,
        DateTime attemptedAt)
    {
        var storedResponseBody = BuildStoredResponseBody(result.ResponseBody, out var responseBodyTruncated);

        return new DeliveryAttempt
        {
            TenantId = incomingEvent.TenantId,
            EventId = incomingEvent.EventId,
            SubscriptionId = subscription.Id,
            EventType = incomingEvent.EventType,
            TargetUrl = subscription.TargetUrl,
            AttemptNumber = attemptNumber,
            Status = result.IsSuccess ? DeliveryStatus.Success : DeliveryStatus.Failed,
            HttpStatusCode = result.HttpStatusCode,
            ResponseBody = storedResponseBody,
            ResponseBodyTruncated = responseBodyTruncated,
            ErrorMessage = result.ErrorMessage,
            DurationMs = result.DurationMs,
            AttemptedAt = attemptedAt,
            CorrelationId = correlationId,
            CreatedAt = attemptedAt,
            UpdatedAt = null,
        };
    }


    private static string? BuildStoredResponseBody(string? responseBody, out bool isTruncated)
    {
        isTruncated = false;

        if (string.IsNullOrEmpty(responseBody)
            || responseBody.Length <= ValidationLimits.MaxResponseBodyStoredLength)
        {
            return responseBody;
        }

        isTruncated = true;
        return responseBody[..ValidationLimits.MaxResponseBodyStoredLength];
    }

    private async Task TryScheduleRetryAsync(
        IncomingEvent incomingEvent,
        Subscription subscription,
        WebhookDeliveryResult result,
        int currentAttemptNumber,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var retryPolicy = new RetryPolicyDto
        {
            MaxAttempts = subscription.RetryPolicy.MaxAttempts,
            InitialDelaySeconds = subscription.RetryPolicy.InitialDelaySeconds,
            BackoffType = subscription.RetryPolicy.BackoffType,
        };

        if (!retryPolicyService.ShouldRetry(retryPolicy, currentAttemptNumber))
        {
            await MoveToDlqAsync(incomingEvent, subscription, result, currentAttemptNumber, correlationId, cancellationToken);
            return;
        }

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
            CorrelationId = correlationId,
        };

        try
        {
            await (tracingService?.CaptureSpanAsync(
                "Publish retry",
                "messaging.kafka",
                () => kafkaProducer.ProduceAsync(
                    KafkaTopics.WebhookRetry,
                    incomingEvent.TenantId,
                    retryMessage,
                    cancellationToken))
                ?? kafkaProducer.ProduceAsync(
                    KafkaTopics.WebhookRetry,
                    incomingEvent.TenantId,
                    retryMessage,
                    cancellationToken));

            logger.LogInformation(
                "Retry rescheduled. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, AttemptNumber: {AttemptNumber}, NextRetryAt: {NextRetryAt}, DelaySeconds: {DelaySeconds}, CorrelationId: {CorrelationId}",
                incomingEvent.TenantId,
                incomingEvent.EventId,
                subscription.Id,
                nextAttemptNumber,
                nextRetryAt,
                retryDelay.TotalSeconds,
                correlationId);
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
                correlationId);
        }
    }

    private async Task MoveToDlqAsync(
        IncomingEvent incomingEvent,
        Subscription subscription,
        WebhookDeliveryResult result,
        int finalAttemptNumber,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await usageService.IncrementEventsFailedAsync(incomingEvent.TenantId, cancellationToken);

        var failedAt = dateTimeProvider.UtcNow;
        var failedEvent = new FailedEvent
        {
            TenantId = incomingEvent.TenantId,
            EventId = incomingEvent.EventId,
            SubscriptionId = subscription.Id,
            EventType = incomingEvent.EventType,
            TargetUrl = subscription.TargetUrl,
            Reason = "Retry attempts exhausted",
            FinalAttemptNumber = finalAttemptNumber,
            LastHttpStatusCode = result.HttpStatusCode,
            LastErrorMessage = result.ErrorMessage,
            Status = "DLQ",
            FailedAt = failedAt,
            CorrelationId = correlationId,
            InternalEvent = new
            {
                id = incomingEvent.EventId,
                eventType = incomingEvent.EventType,
                timestamp = incomingEvent.SourceTimestamp ?? incomingEvent.ReceivedAt,
                payload = incomingEvent.Payload,
            },
            CreatedAt = failedAt,
            UpdatedAt = null,
        };

        var dlqMessage = new WebhookDlqMessage
        {
            EventId = incomingEvent.EventId,
            TenantId = incomingEvent.TenantId,
            SubscriptionId = subscription.Id,
            Reason = failedEvent.Reason,
            FinalAttemptNumber = finalAttemptNumber,
            CorrelationId = correlationId,
        };

        try
        {
            await (tracingService?.CaptureSpanAsync(
                "Publish DLQ",
                "messaging.kafka",
                () => kafkaProducer.ProduceAsync(
                    KafkaTopics.WebhookDlq,
                    incomingEvent.TenantId,
                    dlqMessage,
                    cancellationToken))
                ?? kafkaProducer.ProduceAsync(
                    KafkaTopics.WebhookDlq,
                    incomingEvent.TenantId,
                    dlqMessage,
                    cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish DLQ webhook message. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, EventType: {EventType}, FinalAttemptNumber: {FinalAttemptNumber}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                incomingEvent.TenantId,
                incomingEvent.EventId,
                subscription.Id,
                incomingEvent.EventType,
                finalAttemptNumber,
                failedEvent.Reason,
                correlationId);
        }

        try
        {
            await failedEventService.CreateAsync(failedEvent, cancellationToken);
            logger.LogInformation(
                "Webhook moved to DLQ and stored as failed event. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, EventType: {EventType}, FinalAttemptNumber: {FinalAttemptNumber}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                incomingEvent.TenantId,
                incomingEvent.EventId,
                subscription.Id,
                incomingEvent.EventType,
                finalAttemptNumber,
                failedEvent.Reason,
                correlationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to store failed event after retry exhaustion. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, EventType: {EventType}, FinalAttemptNumber: {FinalAttemptNumber}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                incomingEvent.TenantId,
                incomingEvent.EventId,
                subscription.Id,
                incomingEvent.EventType,
                finalAttemptNumber,
                failedEvent.Reason,
                correlationId);
        }
    }

    private static WebhookDeliveryRequest BuildRequest(Subscription subscription, IncomingEvent incomingEvent, string? correlationId, ISecretEncryptionService secretEncryptionService)
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
                    Password = secretEncryptionService.Decrypt(subscription.Authentication.Basic.Password),
                },
                OAuth2 = subscription.Authentication.OAuth2 is null ? null : new OAuth2ClientCredentialsDto
                {
                    TokenUrl = subscription.Authentication.OAuth2.TokenUrl,
                    ClientId = subscription.Authentication.OAuth2.ClientId,
                    ClientSecret = secretEncryptionService.Decrypt(subscription.Authentication.OAuth2.ClientSecret),
                    Scope = subscription.Authentication.OAuth2.Scope,
                },
                ApiKeyHeader = subscription.Authentication.ApiKeyHeader is null ? null : new ApiKeyHeaderDto
                {
                    HeaderName = subscription.Authentication.ApiKeyHeader.HeaderName,
                    HeaderValue = secretEncryptionService.Decrypt(subscription.Authentication.ApiKeyHeader.HeaderValue),
                },
                HmacSignature = subscription.Authentication.HmacSignature is null ? null : new HmacSignatureDto
                {
                    Secret = secretEncryptionService.Decrypt(subscription.Authentication.HmacSignature.Secret),
                    HeaderName = subscription.Authentication.HmacSignature.HeaderName,
                    Algorithm = subscription.Authentication.HmacSignature.Algorithm,
                },
            },
            TimeoutSeconds = subscription.TimeoutSeconds,
            CorrelationId = correlationId,
        };
    }
}
