using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class DeliveryAttemptService(IDeliveryAttemptRepository deliveryAttemptRepository) : IDeliveryAttemptService
{
    public async Task<PagedResponseDto<DeliveryAttemptResponseDto>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var descending = request.NormalizedSortDirection == "desc";
        var sort = GetSortDefinition(request.SortBy, descending);

        var result = await deliveryAttemptRepository.SearchAsync(request, sort, request.Skip, pageSize, cancellationToken);
        return PagedResponseDto<DeliveryAttemptResponseDto>.Create(result.Items.Select(Map).ToList(), pageNumber, pageSize, result.TotalCount);
    }

    private static SortDefinition<DeliveryAttempt> GetSortDefinition(string? sortBy, bool descending)
    {
        var sortBuilder = Builders<DeliveryAttempt>.Sort;
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "attemptedat" => descending ? sortBuilder.Descending(x => x.AttemptedAt) : sortBuilder.Ascending(x => x.AttemptedAt),
            "eventtype" => descending ? sortBuilder.Descending(x => x.EventType) : sortBuilder.Ascending(x => x.EventType),
            "status" => descending ? sortBuilder.Descending(x => x.Status) : sortBuilder.Ascending(x => x.Status),
            "httpstatuscode" => descending ? sortBuilder.Descending(x => x.HttpStatusCode) : sortBuilder.Ascending(x => x.HttpStatusCode),
            "durationms" => descending ? sortBuilder.Descending(x => x.DurationMs) : sortBuilder.Ascending(x => x.DurationMs),
            _ => sortBuilder.Descending(x => x.AttemptedAt),
        };
    }

    public async Task<DeliveryAttemptResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await deliveryAttemptRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    private static DeliveryAttemptResponseDto Map(DeliveryAttempt entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        EventId = entity.EventId,
        SubscriptionId = entity.SubscriptionId,
        EventType = entity.EventType,
        TargetUrl = entity.TargetUrl,
        AttemptNumber = entity.AttemptNumber,
        Status = entity.Status,
        HttpStatusCode = entity.HttpStatusCode,
        ResponseBody = entity.ResponseBody,
        ResponseBodyTruncated = entity.ResponseBodyTruncated,
        ErrorMessage = entity.ErrorMessage,
        DurationMs = entity.DurationMs,
        AttemptedAt = entity.AttemptedAt,
        CorrelationId = entity.CorrelationId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
