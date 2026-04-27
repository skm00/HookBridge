using HookBridge.Application.DTOs.DeliveryAttempts;

namespace HookBridge.Application.Interfaces.Services;

public interface IDeliveryAttemptService
{
    Task<IReadOnlyList<DeliveryAttemptResponseDto>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DeliveryAttemptResponseDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}
