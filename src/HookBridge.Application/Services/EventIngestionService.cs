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
using System.Text.Json;

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

        var effectiveEventId = string.IsNullOrWhiteSpace(request.EventId)
            ? guidGenerator.NewGuid()
            : request.EventId;

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
            x => x.TenantId == tenantId && x.EventId == effectiveEventId,
            cancellationToken);

        if (duplicate is not null)
        {
            logger.LogInformation(
                "Duplicate event accepted for tenant {TenantId}. EventId: {EventId}. EventType: {EventType}. CorrelationId: {CorrelationId}",
                tenantId,
                effectiveEventId,
                request.EventType,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = effectiveEventId,
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
            EventId = effectiveEventId,
            SourceTimestamp = request.Timestamp,
            Payload = NormalizePayload(request.Data),
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
                effectiveEventId,
                request.EventType,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = effectiveEventId,
                Message = "Event accepted for delivery.",
            };
        }
        catch (Exception ex)
        {
            incomingEvent.Status = "QueuePublishFailed";
            incomingEvent.UpdatedAt = dateTimeProvider.UtcNow;
            await incomingEventRepository.UpdateAsync(incomingEvent, cancellationToken);

            logger.LogError(
                ex,
                "Event accepted but Kafka publishing failed for tenant {TenantId}. EventId: {EventId}. CorrelationId: {CorrelationId}",
                tenantId,
                effectiveEventId,
                correlationId);

            return new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = effectiveEventId,
                Message = "Event accepted but publishing is delayed.",
            };
        }
    }

    private static object? NormalizePayload(object? payload)
    {
        if (payload is JsonElement element)
        {
            return NormalizeJsonElement(element);
        }

        return payload;
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

}
