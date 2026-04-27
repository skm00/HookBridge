using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Interfaces.Services;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default);

    Task<SubscriptionResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default);
}
