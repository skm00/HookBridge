using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Services;

public sealed class IncomingEventQueryService(IMongoRepository<IncomingEvent> incomingEventRepository) : IIncomingEventQueryService
{
    public async Task<IReadOnlyList<IncomingEventResponseDto>> SearchAsync(
        IncomingEventSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var items = await incomingEventRepository.FindAsync(
            x =>
                (string.IsNullOrWhiteSpace(request.TenantId) || x.TenantId == request.TenantId)
                && (string.IsNullOrWhiteSpace(request.EventId) || x.EventId == request.EventId)
                && (string.IsNullOrWhiteSpace(request.EventType) || x.EventType == request.EventType)
                && (string.IsNullOrWhiteSpace(request.Status) || x.Status == request.Status)
                && (request.FromDate == null || x.ReceivedAt >= request.FromDate.Value)
                && (request.ToDate == null || x.ReceivedAt <= request.ToDate.Value)
                && (string.IsNullOrWhiteSpace(request.CorrelationId) || x.CorrelationId == request.CorrelationId),
            cancellationToken);

        return items
            .OrderByDescending(x => x.ReceivedAt)
            .Take(500)
            .Select(Map)
            .ToList();
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
