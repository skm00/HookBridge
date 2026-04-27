using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IDeliveryAttemptRepository
{
    Task<IReadOnlyList<DeliveryAttempt>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
