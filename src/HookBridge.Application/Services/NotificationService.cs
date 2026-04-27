using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class NotificationService(
    INotificationRepository notificationRepository,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider) : INotificationService
{
    public Task CreateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.Id = string.IsNullOrWhiteSpace(notification.Id) ? guidGenerator.NewGuid() : notification.Id;
        notification.CreatedAt = notification.CreatedAt == default ? dateTimeProvider.UtcNow : notification.CreatedAt;
        notification.UpdatedAt = null;
        return notificationRepository.AddAsync(notification, cancellationToken);
    }

    public async Task<PagedResponseDto<NotificationResponseDto>> SearchAsync(
        NotificationSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;
        var sort = Builders<Notification>.Sort.Descending(x => x.CreatedAt);

        var result = await notificationRepository.SearchAsync(request, sort, request.Skip, pageSize, cancellationToken);
        return PagedResponseDto<NotificationResponseDto>.Create(result.Items.Select(Map).ToList(), pageNumber, pageSize, result.TotalCount);
    }

    public async Task<NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        return notification is null ? null : Map(notification);
    }

    public async Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationRepository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return false;
        }

        if (notification.IsRead)
        {
            return true;
        }

        var now = dateTimeProvider.UtcNow;
        notification.IsRead = true;
        notification.ReadAt = now;
        notification.UpdatedAt = now;
        await notificationRepository.UpdateAsync(notification, cancellationToken);
        return true;
    }

    public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
        => notificationRepository.GetUnreadCountAsync(tenantId, cancellationToken);

    private static NotificationResponseDto Map(Notification notification) => new()
    {
        Id = notification.Id,
        TenantId = notification.TenantId,
        Type = notification.Type,
        Severity = notification.Severity,
        Title = notification.Title,
        Message = notification.Message,
        ResourceType = notification.ResourceType,
        ResourceId = notification.ResourceId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt,
    };
}
