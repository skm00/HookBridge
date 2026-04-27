using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.DeliveryAttempts;

namespace HookBridge.Application.Interfaces.Services;

public interface IDeliveryAttemptService
{
    Task<PagedResponseDto<DeliveryAttemptResponseDto>> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DeliveryAttemptResponseDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}
