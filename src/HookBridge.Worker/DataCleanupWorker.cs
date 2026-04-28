using HookBridge.Application.Interfaces.Services;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Worker;

public class DataCleanupWorker(
    IDataCleanupService dataCleanupService,
    IOptions<DataRetentionSettings> dataRetentionOptions,
    ILogger<DataCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupCycleAsync(stoppingToken);

            try
            {
                await DelayForNextRunAsync(RunInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RunCleanupCycleAsync(CancellationToken cancellationToken)
    {
        var settings = dataRetentionOptions.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Data retention cleanup is disabled. Skipping cleanup cycle.");
            return;
        }

        await CleanupWithLoggingAsync("IncomingEvent", settings.IncomingEventsDays, dataCleanupService.CleanupIncomingEventsAsync, cancellationToken);
        await CleanupWithLoggingAsync("DeliveryAttempt", settings.DeliveryLogsDays, dataCleanupService.CleanupDeliveryLogsAsync, cancellationToken);
        await CleanupWithLoggingAsync("FailedEvent", settings.FailedEventsDays, dataCleanupService.CleanupFailedEventsAsync, cancellationToken);
        await CleanupWithLoggingAsync("AuditLog", settings.AuditLogsDays, dataCleanupService.CleanupAuditLogsAsync, cancellationToken);
        await CleanupWithLoggingAsync("Notification", settings.NotificationsDays, dataCleanupService.CleanupNotificationsAsync, cancellationToken);
    }

    protected virtual Task DelayForNextRunAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);

    private async Task CleanupWithLoggingAsync(
        string entityType,
        int retentionDays,
        Func<int, CancellationToken, Task<long>> cleanupAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedCount = await cleanupAction(retentionDays, cancellationToken);
            logger.LogInformation(
                "Cleanup worker deleted retained data. EntityType: {EntityType}, DeletedCount: {DeletedCount}, RetentionDays: {RetentionDays}",
                entityType,
                deletedCount,
                retentionDays);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Cleanup worker failed for entity. EntityType: {EntityType}, RetentionDays: {RetentionDays}",
                entityType,
                retentionDays);
        }
    }
}
