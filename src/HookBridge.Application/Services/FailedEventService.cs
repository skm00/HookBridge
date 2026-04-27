using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Shared.Constants;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class FailedEventService(
    IFailedEventRepository failedEventRepository,
    IKafkaProducer kafkaProducer,
    IDateTimeProvider dateTimeProvider,
    ILogger<FailedEventService> logger) : IFailedEventService
{
    public Task CreateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
    {
        return failedEventRepository.AddAsync(failedEvent, cancellationToken);
    }

    public async Task<PagedResponseDto<FailedEventResponseDto>> SearchAsync(
        FailedEventSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var descending = request.NormalizedSortDirection == "desc";
        var sort = GetSortDefinition(request.SortBy, descending);

        var result = await failedEventRepository.SearchAsync(request, sort, request.Skip, pageSize, cancellationToken);
        return PagedResponseDto<FailedEventResponseDto>.Create(result.Items.Select(Map).ToList(), pageNumber, pageSize, result.TotalCount);
    }

    private static SortDefinition<FailedEvent> GetSortDefinition(string? sortBy, bool descending)
    {
        var sortBuilder = Builders<FailedEvent>.Sort;
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "failedat" => descending ? sortBuilder.Descending(x => x.FailedAt) : sortBuilder.Ascending(x => x.FailedAt),
            "eventtype" => descending ? sortBuilder.Descending(x => x.EventType) : sortBuilder.Ascending(x => x.EventType),
            "status" => descending ? sortBuilder.Descending(x => x.Status) : sortBuilder.Ascending(x => x.Status),
            "finalattemptnumber" => descending ? sortBuilder.Descending(x => x.FinalAttemptNumber) : sortBuilder.Ascending(x => x.FinalAttemptNumber),
            _ => sortBuilder.Descending(x => x.FailedAt),
        };
    }

    public async Task<FailedEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await failedEventRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<bool> RetryAsync(string failedEventId, CancellationToken cancellationToken = default)
    {
        var failedEvent = await failedEventRepository.GetByIdAsync(failedEventId, cancellationToken);
        if (failedEvent is null)
        {
            return false;
        }

        if (!string.Equals(failedEvent.Status, "DLQ", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Manual retry rejected because failed event is not in DLQ state. FailedEventId: {FailedEventId}, TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, CorrelationId: {CorrelationId}, CurrentStatus: {CurrentStatus}",
                failedEvent.Id,
                failedEvent.TenantId,
                failedEvent.EventId,
                failedEvent.SubscriptionId,
                failedEvent.CorrelationId,
                failedEvent.Status);
            return false;
        }

        var now = dateTimeProvider.UtcNow;
        var retryMessage = new WebhookRetryMessage
        {
            EventId = failedEvent.EventId,
            TenantId = failedEvent.TenantId,
            SubscriptionId = failedEvent.SubscriptionId,
            AttemptNumber = 1,
            NextRetryAt = now,
            CorrelationId = failedEvent.CorrelationId,
        };

        try
        {
            await kafkaProducer.ProduceAsync(KafkaTopics.WebhookRetry, failedEvent.TenantId, retryMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Manual retry publish failed. FailedEventId: {FailedEventId}, TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, CorrelationId: {CorrelationId}",
                failedEvent.Id,
                failedEvent.TenantId,
                failedEvent.EventId,
                failedEvent.SubscriptionId,
                failedEvent.CorrelationId);
            throw;
        }

        failedEvent.Status = "RetryRequested";
        failedEvent.UpdatedAt = now;
        await failedEventRepository.UpdateAsync(failedEvent, cancellationToken);

        logger.LogInformation(
            "Manual retry requested for failed event. FailedEventId: {FailedEventId}, TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, CorrelationId: {CorrelationId}",
            failedEvent.Id,
            failedEvent.TenantId,
            failedEvent.EventId,
            failedEvent.SubscriptionId,
            failedEvent.CorrelationId);

        return true;
    }

    private static FailedEventResponseDto Map(FailedEvent entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        EventId = entity.EventId,
        SubscriptionId = entity.SubscriptionId,
        EventType = entity.EventType,
        TargetUrl = entity.TargetUrl,
        Reason = entity.Reason,
        FinalAttemptNumber = entity.FinalAttemptNumber,
        LastHttpStatusCode = entity.LastHttpStatusCode,
        LastErrorMessage = entity.LastErrorMessage,
        Status = entity.Status,
        FailedAt = entity.FailedAt,
        CorrelationId = entity.CorrelationId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
