using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Services;

public sealed class DeliveryAttemptService(IDeliveryAttemptRepository deliveryAttemptRepository) : IDeliveryAttemptService
{
    public async Task<IReadOnlyList<DeliveryAttemptResponseDto>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var items = await deliveryAttemptRepository.SearchAsync(request, cancellationToken);
        return items.Select(Map).ToList();
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
