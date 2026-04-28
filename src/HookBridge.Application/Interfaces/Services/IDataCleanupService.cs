namespace HookBridge.Application.Interfaces.Services;

public interface IDataCleanupService
{
    Task<long> CleanupIncomingEventsAsync(int retentionDays, CancellationToken cancellationToken = default);

    Task<long> CleanupDeliveryLogsAsync(int retentionDays, CancellationToken cancellationToken = default);

    Task<long> CleanupFailedEventsAsync(int retentionDays, CancellationToken cancellationToken = default);

    Task<long> CleanupAuditLogsAsync(int retentionDays, CancellationToken cancellationToken = default);

    Task<long> CleanupNotificationsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
