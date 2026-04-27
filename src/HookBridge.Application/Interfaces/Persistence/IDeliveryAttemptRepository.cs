using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IDeliveryAttemptRepository
{
    Task<IReadOnlyList<DeliveryAttempt>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        string tenantId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        DeliveryStatus? status,
        CancellationToken cancellationToken = default);
}
