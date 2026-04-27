using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Interfaces.Services;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default);

    Task<SubscriptionResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<SubscriptionResponseDto?> UpdateAsync(string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> EnableAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(string id, CancellationToken cancellationToken = default);
}
