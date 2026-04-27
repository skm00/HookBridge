using FluentValidation;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Domain.Entities;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class EventIngestionService(
    IMongoRepository<IncomingEvent> incomingEventRepository,
    IApiKeyService apiKeyService,
    IUsageService usageService,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IValidator<EventIngestionRequestDto> validator,
    IKafkaProducer kafkaProducer,
    ILogger<EventIngestionService> logger) : IEventIngestionService
{
    public async Task<EventIngestionResponseDto> IngestAsync(
        string tenantId,
        string apiKey,
        EventIngestionRequestDto request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var validationResult = await apiKeyService.ValidateAsync(tenantId, apiKey, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning(
                "Event ingestion unauthorized for tenant {TenantId}. Reason: {Reason}. CorrelationId: {CorrelationId}",
                tenantId,
                validationResult.FailureReason,
                correlationId);

            throw new UnauthorizedException("Invalid API key.");
        }

        var duplicate = await incomingEventRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.EventId == request.EventId,
            cancellationToken);

        if (duplicate is not null)
        {
            logger.LogInformation(
                "Duplicate event accepted for tenant {TenantId}. EventId: {EventId}. EventType: {EventType}. CorrelationId: {CorrelationId}",
                tenantId,
                request.EventId,
                request.EventType,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = request.EventId,
                Message = "Event already accepted.",
            };
        }

        var canAccept = await usageService.CanAcceptEventAsync(tenantId, cancellationToken);
        if (!canAccept)
        {
            throw new TooManyRequestsException("Monthly event limit exceeded for the current billing plan.");
        }

        var now = dateTimeProvider.UtcNow;
        var incomingEvent = new IncomingEvent
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            EventType = request.EventType,
            EventId = request.EventId,
            SourceTimestamp = request.Timestamp,
            Payload = request.Data,
            Status = "Accepted",
            ReceivedAt = now,
            ApiKeyId = validationResult.ApiKeyId,
            CorrelationId = correlationId,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await incomingEventRepository.AddAsync(incomingEvent, cancellationToken);
        await usageService.IncrementEventsReceivedAsync(tenantId, cancellationToken);

        try
        {
            var message = new WebhookEventMessage
            {
                EventId = incomingEvent.EventId,
                TenantId = incomingEvent.TenantId,
                EventType = incomingEvent.EventType,
                ReceivedAt = incomingEvent.ReceivedAt,
                CorrelationId = incomingEvent.CorrelationId,
            };

            await kafkaProducer.ProduceAsync(KafkaTopics.WebhookEvents, incomingEvent.TenantId, message, cancellationToken);

            logger.LogInformation(
                "Event accepted and queued for tenant {TenantId}. EventId: {EventId}. EventType: {EventType}. CorrelationId: {CorrelationId}",
                tenantId,
                request.EventId,
                request.EventType,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = request.EventId,
                Message = "Event accepted for delivery.",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Event accepted but Kafka publishing failed for tenant {TenantId}. EventId: {EventId}. CorrelationId: {CorrelationId}",
                tenantId,
                request.EventId,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = request.EventId,
                Message = "Event accepted but publishing is delayed.",
            };
        }
    }
}
