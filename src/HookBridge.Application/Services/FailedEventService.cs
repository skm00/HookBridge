using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Services;

public sealed class FailedEventService(IFailedEventRepository failedEventRepository) : IFailedEventService
{
    public Task CreateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
    {
        return failedEventRepository.AddAsync(failedEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<FailedEventResponseDto>> SearchAsync(
        FailedEventSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var items = await failedEventRepository.SearchAsync(request, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<FailedEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await failedEventRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
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
