using HookBridge.Application.DTOs.Notifications;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Interfaces.Persistence;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Notification> Items, long TotalCount)> SearchAsync(
        NotificationSearchRequestDto request,
        SortDefinition<Notification> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string tenantId,
        string type,
        DateTime fromInclusive,
        DateTime toExclusive,
        CancellationToken cancellationToken = default);
}
