using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentValidation;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Models;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Services;

public sealed class BackupService(
    IMongoRepository<Tenant> tenantRepository,
    IMongoRepository<Subscription> subscriptionRepository,
    IMongoRepository<ApiKey> apiKeyRepository,
    IMongoRepository<IncomingEvent> incomingEventRepository,
    IMongoRepository<FailedEvent> failedEventRepository,
    IMongoRepository<Notification> notificationRepository,
    IMongoRepository<AuditLog> auditLogRepository,
    IDateTimeProvider dateTimeProvider,
    ILogger<BackupService> logger) : IBackupService
{
    private const int DefaultEventExportLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<byte[]> ExportAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ValidationException("TenantId is required.");
        }

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' not found.");

        var package = new TenantBackupPackage
        {
            ExportedAtUtc = dateTimeProvider.UtcNow,
            TenantId = tenantId,
            Tenant = tenant,
            Subscriptions = (await subscriptionRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).ToList(),
            ApiKeys = (await apiKeyRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).ToList(),
            Events = (await incomingEventRepository.QueryAsync(
                x => x.TenantId == tenantId,
                Builders<IncomingEvent>.Sort.Descending(x => x.ReceivedAt),
                0,
                DefaultEventExportLimit,
                cancellationToken)).Items.ToList(),
            FailedEvents = (await failedEventRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).ToList(),
            Notifications = (await notificationRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).ToList(),
            AuditLogs = (await auditLogRepository.FindAsync(x => x.TenantId == tenantId, cancellationToken)).ToList(),
        };

        var json = JsonSerializer.Serialize(package, JsonOptions);
        logger.LogInformation("Tenant backup export generated for TenantId={TenantId}.", tenantId);
        return Compress(Encoding.UTF8.GetBytes(json));
    }

    public async Task ImportAsync(string tenantId, byte[] data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ValidationException("TenantId is required.");
        }

        if (data.Length == 0)
        {
            throw new ValidationException("Backup file is empty.");
        }

        var jsonBytes = TryDecompress(data);
        var package = JsonSerializer.Deserialize<TenantBackupPackage>(jsonBytes, JsonOptions)
            ?? throw new ValidationException("Backup payload is invalid.");

        ValidateTenantConsistency(tenantId, package);

        await ImportTenantAsync(package.Tenant, cancellationToken);
        await ImportCollectionAsync(package.Subscriptions, x => x.Id, subscriptionRepository, cancellationToken);
        await ImportCollectionAsync(package.ApiKeys, x => x.Id, apiKeyRepository, cancellationToken);
        await ImportCollectionAsync(package.Events, x => x.Id, incomingEventRepository, cancellationToken);
        await ImportCollectionAsync(package.FailedEvents, x => x.Id, failedEventRepository, cancellationToken);
        await ImportCollectionAsync(package.Notifications, x => x.Id, notificationRepository, cancellationToken);
        await ImportCollectionAsync(package.AuditLogs, x => x.Id, auditLogRepository, cancellationToken);

        logger.LogInformation("Tenant backup restore completed for TenantId={TenantId}.", tenantId);
    }

    private static void ValidateTenantConsistency(string tenantId, TenantBackupPackage package)
    {
        if (!string.Equals(package.TenantId, tenantId, StringComparison.Ordinal))
        {
            throw new ValidationException("Backup tenant does not match requested tenant.");
        }

        if (package.Tenant is null || !string.Equals(package.Tenant.Id, tenantId, StringComparison.Ordinal))
        {
            throw new ValidationException("Backup payload tenant record is invalid.");
        }

        static bool HasMismatch<T>(IEnumerable<T> items, Func<T, string> selector, string tenantIdToMatch)
            => items.Any(item => !string.Equals(selector(item), tenantIdToMatch, StringComparison.Ordinal));

        if (HasMismatch(package.Subscriptions, x => x.TenantId, tenantId)
            || HasMismatch(package.ApiKeys, x => x.TenantId, tenantId)
            || HasMismatch(package.Events, x => x.TenantId, tenantId)
            || HasMismatch(package.FailedEvents, x => x.TenantId, tenantId)
            || HasMismatch(package.Notifications, x => x.TenantId, tenantId)
            || HasMismatch(package.AuditLogs, x => x.TenantId, tenantId))
        {
            throw new ValidationException("Backup contains records for a different tenant.");
        }
    }

    private async Task ImportTenantAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var existing = await tenantRepository.GetByIdAsync(tenant.Id, cancellationToken);
        if (existing is null)
        {
            await tenantRepository.AddAsync(tenant, cancellationToken);
            return;
        }

        // No overwrite by default.
    }

    private static async Task ImportCollectionAsync<T>(
        IEnumerable<T> entities,
        Func<T, string> idSelector,
        IMongoRepository<T> repository,
        CancellationToken cancellationToken)
        where T : BaseEntity
    {
        foreach (var entity in entities)
        {
            var existing = await repository.GetByIdAsync(idSelector(entity), cancellationToken);
            if (existing is null)
            {
                await repository.AddAsync(entity, cancellationToken);
            }
        }
    }

    private static byte[] Compress(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzip = new GZipStream(outputStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }

        return outputStream.ToArray();
    }

    private static byte[] TryDecompress(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        return data;
    }
}
