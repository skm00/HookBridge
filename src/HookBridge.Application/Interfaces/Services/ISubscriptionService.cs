using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Interfaces.Services;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateAsync(string tenantId, CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default);

    Task<SubscriptionResponseDto?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<SubscriptionResponseDto?> UpdateAsync(string tenantId, string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<bool> EnableAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
