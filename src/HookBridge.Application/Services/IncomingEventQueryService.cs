using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class IncomingEventQueryService(IMongoRepository<IncomingEvent> incomingEventRepository) : IIncomingEventQueryService
{
    public async Task<PagedResponseDto<IncomingEventResponseDto>> SearchAsync(
        IncomingEventSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var descending = request.NormalizedSortDirection == "desc";

        var result = await incomingEventRepository.QueryAsync(
            x =>
                (string.IsNullOrWhiteSpace(request.TenantId) || x.TenantId == request.TenantId)
                && (string.IsNullOrWhiteSpace(request.EventId) || x.EventId == request.EventId)
                && (string.IsNullOrWhiteSpace(request.EventType) || x.EventType == request.EventType)
                && (string.IsNullOrWhiteSpace(request.Status) || x.Status == request.Status)
                && (request.FromDate == null || x.ReceivedAt >= request.FromDate.Value)
                && (request.ToDate == null || x.ReceivedAt <= request.ToDate.Value)
                && (string.IsNullOrWhiteSpace(request.CorrelationId) || x.CorrelationId == request.CorrelationId),
            GetSortDefinition(request.SortBy, descending),
            request.Skip,
            pageSize,
            cancellationToken);

        return PagedResponseDto<IncomingEventResponseDto>.Create(result.Items.Select(Map).ToList(), pageNumber, pageSize, result.TotalCount);
    }

    private static SortDefinition<IncomingEvent> GetSortDefinition(string? sortBy, bool descending)
    {
        var sortBuilder = Builders<IncomingEvent>.Sort;
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "receivedat" => descending ? sortBuilder.Descending(x => x.ReceivedAt) : sortBuilder.Ascending(x => x.ReceivedAt),
            "eventtype" => descending ? sortBuilder.Descending(x => x.EventType) : sortBuilder.Ascending(x => x.EventType),
            "status" => descending ? sortBuilder.Descending(x => x.Status) : sortBuilder.Ascending(x => x.Status),
            _ => sortBuilder.Descending(x => x.ReceivedAt),
        };
    }

    public async Task<IncomingEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await incomingEventRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    private static IncomingEventResponseDto Map(IncomingEvent entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        EventId = entity.EventId,
        EventType = entity.EventType,
        SourceTimestamp = entity.SourceTimestamp,
        Status = entity.Status,
        ReceivedAt = entity.ReceivedAt,
        ApiKeyId = entity.ApiKeyId,
        CorrelationId = entity.CorrelationId,
        Payload = entity.Payload,
    };
}
