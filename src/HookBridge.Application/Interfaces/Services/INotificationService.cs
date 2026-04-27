using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Notifications;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Services;

public interface INotificationService
{
    Task CreateAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<PagedResponseDto<NotificationResponseDto>> SearchAsync(NotificationSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default);
}
