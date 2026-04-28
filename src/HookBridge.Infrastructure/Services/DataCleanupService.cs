using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Services;

public sealed class DataCleanupService(
    IMongoDatabase database,
    IDateTimeProvider dateTimeProvider,
    ILogger<DataCleanupService> logger) : IDataCleanupService
{
    private static readonly TimeSpan MinimumDataAge = TimeSpan.FromHours(24);
    private const int WarningRetentionDaysThreshold = 7;

    public Task<long> CleanupIncomingEventsAsync(int retentionDays, CancellationToken cancellationToken = default)
        => CleanupAsync<IncomingEvent>(retentionDays, entity => entity.ReceivedAt, cancellationToken);

    public Task<long> CleanupDeliveryLogsAsync(int retentionDays, CancellationToken cancellationToken = default)
        => CleanupAsync<DeliveryAttempt>(retentionDays, entity => entity.AttemptedAt, cancellationToken);

    public Task<long> CleanupFailedEventsAsync(int retentionDays, CancellationToken cancellationToken = default)
        => CleanupAsync<FailedEvent>(retentionDays, entity => entity.FailedAt, cancellationToken);

    public Task<long> CleanupAuditLogsAsync(int retentionDays, CancellationToken cancellationToken = default)
        => CleanupAsync<AuditLog>(retentionDays, entity => entity.CreatedAt, cancellationToken);

    public Task<long> CleanupNotificationsAsync(int retentionDays, CancellationToken cancellationToken = default)
        => CleanupAsync<Notification>(retentionDays, entity => entity.CreatedAt, cancellationToken);

    private async Task<long> CleanupAsync<TEntity>(
        int retentionDays,
        System.Linq.Expressions.Expression<Func<TEntity, DateTime>> timestampSelector,
        CancellationToken cancellationToken)
    {
        if (retentionDays < WarningRetentionDaysThreshold)
        {
            logger.LogWarning(
                "RetentionDays value is lower than recommended threshold. EntityType: {EntityType}, RetentionDays: {RetentionDays}, RecommendedMinimumDays: {RecommendedMinimumDays}",
                typeof(TEntity).Name,
                retentionDays,
                WarningRetentionDaysThreshold);
        }

        var cutoffDate = ComputeCutoffDate(retentionDays);
        var filter = Builders<TEntity>.Filter.Lt(timestampSelector, cutoffDate);

        var collection = database.GetCollection<TEntity>(typeof(TEntity).Name);
        var deleteResult = await collection.DeleteManyAsync(filter, cancellationToken);

        var deletedCount = deleteResult.DeletedCount;

        logger.LogInformation(
            "Data cleanup completed. EntityType: {EntityType}, DeletedCount: {DeletedCount}, RetentionDays: {RetentionDays}, CutoffDateUtc: {CutoffDateUtc}",
            typeof(TEntity).Name,
            deletedCount,
            retentionDays,
            cutoffDate);

        return deletedCount;
    }

    private DateTime ComputeCutoffDate(int retentionDays)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var retentionCutoff = utcNow.AddDays(-retentionDays);
        var minimumSafeCutoff = utcNow.Subtract(MinimumDataAge);

        return retentionCutoff <= minimumSafeCutoff
            ? retentionCutoff
            : minimumSafeCutoff;
    }
}
