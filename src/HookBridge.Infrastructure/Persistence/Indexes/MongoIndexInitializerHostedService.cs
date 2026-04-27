using HookBridge.Domain.Entities;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Persistence.Indexes;

/// <summary>
/// Creates required MongoDB indexes at startup.
/// </summary>
public sealed class MongoIndexInitializerHostedService(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tenants = database.GetCollection<Tenant>(nameof(Tenant));

        var slugIndexKeys = Builders<Tenant>.IndexKeys.Ascending(x => x.Slug);
        var slugIndexOptions = new CreateIndexOptions { Unique = true, Name = "ux_tenant_slug" };
        var slugIndex = new CreateIndexModel<Tenant>(slugIndexKeys, slugIndexOptions);

        await tenants.Indexes.CreateOneAsync(slugIndex, cancellationToken: cancellationToken);

        var adminUsers = database.GetCollection<AdminUser>(nameof(AdminUser));

        var adminTenantIndex = new CreateIndexModel<AdminUser>(
            Builders<AdminUser>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_adminuser_tenantid" });

        var adminEmailIndex = new CreateIndexModel<AdminUser>(
            Builders<AdminUser>.IndexKeys.Ascending(x => x.Email),
            new CreateIndexOptions { Name = "ix_adminuser_email" });

        var adminTenantEmailUniqueIndex = new CreateIndexModel<AdminUser>(
            Builders<AdminUser>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.Email),
            new CreateIndexOptions { Unique = true, Name = "ux_adminuser_tenantid_email" });

        await adminUsers.Indexes.CreateManyAsync(
            [adminTenantIndex, adminEmailIndex, adminTenantEmailUniqueIndex],
            cancellationToken);

        var apiKeys = database.GetCollection<ApiKey>(nameof(ApiKey));

        var hashUniqueIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(x => x.KeyHash),
            new CreateIndexOptions { Unique = true, Name = "ux_apikey_keyhash" });

        var tenantIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_apikey_tenantid" });

        var tenantActiveIndex = new CreateIndexModel<ApiKey>(
            Builders<ApiKey>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsActive),
            new CreateIndexOptions { Name = "ix_apikey_tenantid_isactive" });

        await apiKeys.Indexes.CreateManyAsync([hashUniqueIndex, tenantIndex, tenantActiveIndex], cancellationToken);
        var subscriptions = database.GetCollection<Subscription>(nameof(Subscription));

        var subscriptionTenantIndex = new CreateIndexModel<Subscription>(
            Builders<Subscription>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_subscription_tenantid" });

        var subscriptionEventTypeIndex = new CreateIndexModel<Subscription>(
            Builders<Subscription>.IndexKeys.Ascending(x => x.EventType),
            new CreateIndexOptions { Name = "ix_subscription_eventtype" });

        var subscriptionIsActiveIndex = new CreateIndexModel<Subscription>(
            Builders<Subscription>.IndexKeys.Ascending(x => x.IsActive),
            new CreateIndexOptions { Name = "ix_subscription_isactive" });

        var subscriptionTenantEventActiveIndex = new CreateIndexModel<Subscription>(
            Builders<Subscription>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.EventType)
                .Ascending(x => x.IsActive),
            new CreateIndexOptions { Name = "ix_subscription_tenantid_eventtype_isactive" });

        await subscriptions.Indexes.CreateManyAsync(
            [subscriptionTenantIndex, subscriptionEventTypeIndex, subscriptionIsActiveIndex, subscriptionTenantEventActiveIndex],
            cancellationToken);

        var incomingEvents = database.GetCollection<IncomingEvent>(nameof(IncomingEvent));

        var tenantEventUniqueIndex = new CreateIndexModel<IncomingEvent>(
            Builders<IncomingEvent>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.EventId),
            new CreateIndexOptions { Unique = true, Name = "ux_incomingevent_tenantid_eventid" });

        var eventTenantIndex = new CreateIndexModel<IncomingEvent>(
            Builders<IncomingEvent>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_incomingevent_tenantid" });

        var eventTypeIndex = new CreateIndexModel<IncomingEvent>(
            Builders<IncomingEvent>.IndexKeys.Ascending(x => x.EventType),
            new CreateIndexOptions { Name = "ix_incomingevent_eventtype" });

        var receivedAtIndex = new CreateIndexModel<IncomingEvent>(
            Builders<IncomingEvent>.IndexKeys.Ascending(x => x.ReceivedAt),
            new CreateIndexOptions { Name = "ix_incomingevent_receivedat" });

        var statusIndex = new CreateIndexModel<IncomingEvent>(
            Builders<IncomingEvent>.IndexKeys.Ascending(x => x.Status),
            new CreateIndexOptions { Name = "ix_incomingevent_status" });

        await incomingEvents.Indexes.CreateManyAsync(
            [tenantEventUniqueIndex, eventTenantIndex, eventTypeIndex, receivedAtIndex, statusIndex],
            cancellationToken);

        var deliveryAttempts = database.GetCollection<DeliveryAttempt>(nameof(DeliveryAttempt));

        var deliveryTenantIdIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_deliveryattempt_tenantid" });

        var deliveryEventIdIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys.Ascending(x => x.EventId),
            new CreateIndexOptions { Name = "ix_deliveryattempt_eventid" });

        var deliverySubscriptionIdIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys.Ascending(x => x.SubscriptionId),
            new CreateIndexOptions { Name = "ix_deliveryattempt_subscriptionid" });

        var deliveryStatusIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys.Ascending(x => x.Status),
            new CreateIndexOptions { Name = "ix_deliveryattempt_status" });

        var deliveryAttemptedAtIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys.Ascending(x => x.AttemptedAt),
            new CreateIndexOptions { Name = "ix_deliveryattempt_attemptedat" });

        var deliveryTenantStatusAttemptedAtIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.Status)
                .Ascending(x => x.AttemptedAt),
            new CreateIndexOptions { Name = "ix_deliveryattempt_tenantid_status_attemptedat" });

        var deliveryTenantEventIdIndex = new CreateIndexModel<DeliveryAttempt>(
            Builders<DeliveryAttempt>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.EventId),
            new CreateIndexOptions { Name = "ix_deliveryattempt_tenantid_eventid" });

        await deliveryAttempts.Indexes.CreateManyAsync(
            [
                deliveryTenantIdIndex,
                deliveryEventIdIndex,
                deliverySubscriptionIdIndex,
                deliveryStatusIndex,
                deliveryAttemptedAtIndex,
                deliveryTenantStatusAttemptedAtIndex,
                deliveryTenantEventIdIndex,
            ],
            cancellationToken);

        var failedEvents = database.GetCollection<FailedEvent>(nameof(FailedEvent));

        var failedTenantIdIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_failedevent_tenantid" });

        var failedEventIdIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys.Ascending(x => x.EventId),
            new CreateIndexOptions { Name = "ix_failedevent_eventid" });

        var failedSubscriptionIdIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys.Ascending(x => x.SubscriptionId),
            new CreateIndexOptions { Name = "ix_failedevent_subscriptionid" });

        var failedStatusIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys.Ascending(x => x.Status),
            new CreateIndexOptions { Name = "ix_failedevent_status" });

        var failedAtIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys.Ascending(x => x.FailedAt),
            new CreateIndexOptions { Name = "ix_failedevent_failedat" });

        var failedTenantStatusFailedAtIndex = new CreateIndexModel<FailedEvent>(
            Builders<FailedEvent>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.Status)
                .Ascending(x => x.FailedAt),
            new CreateIndexOptions { Name = "ix_failedevent_tenantid_status_failedat" });

        await failedEvents.Indexes.CreateManyAsync(
            [
                failedTenantIdIndex,
                failedEventIdIndex,
                failedSubscriptionIdIndex,
                failedStatusIndex,
                failedAtIndex,
                failedTenantStatusFailedAtIndex,
            ],
            cancellationToken);

        var usageMetrics = database.GetCollection<UsageMetric>(nameof(UsageMetric));

        var usageUniqueTenantYearMonth = new CreateIndexModel<UsageMetric>(
            Builders<UsageMetric>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.Year)
                .Ascending(x => x.Month),
            new CreateIndexOptions { Unique = true, Name = "ux_usagemetric_tenantid_year_month" });

        var usageTenantIndex = new CreateIndexModel<UsageMetric>(
            Builders<UsageMetric>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_usagemetric_tenantid" });

        var usageYearMonthIndex = new CreateIndexModel<UsageMetric>(
            Builders<UsageMetric>.IndexKeys
                .Ascending(x => x.Year)
                .Ascending(x => x.Month),
            new CreateIndexOptions { Name = "ix_usagemetric_year_month" });

        await usageMetrics.Indexes.CreateManyAsync(
            [usageUniqueTenantYearMonth, usageTenantIndex, usageYearMonthIndex],
            cancellationToken);

        var notifications = database.GetCollection<Notification>(nameof(Notification));

        var notificationTenantIdIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_notification_tenantid" });

        var notificationTypeIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.Type),
            new CreateIndexOptions { Name = "ix_notification_type" });

        var notificationSeverityIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.Severity),
            new CreateIndexOptions { Name = "ix_notification_severity" });

        var notificationIsReadIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.IsRead),
            new CreateIndexOptions { Name = "ix_notification_isread" });

        var notificationCreatedAtIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "ix_notification_createdat" });

        var notificationTenantReadCreatedAtIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.IsRead)
                .Ascending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "ix_notification_tenantid_isread_createdat" });

        await notifications.Indexes.CreateManyAsync(
            [
                notificationTenantIdIndex,
                notificationTypeIndex,
                notificationSeverityIndex,
                notificationIsReadIndex,
                notificationCreatedAtIndex,
                notificationTenantReadCreatedAtIndex,
            ],
            cancellationToken);

        var auditLogs = database.GetCollection<AuditLog>(nameof(AuditLog));

        var auditTenantIdIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.TenantId),
            new CreateIndexOptions { Name = "ix_auditlog_tenantid" });

        var auditUserIdIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.UserId),
            new CreateIndexOptions { Name = "ix_auditlog_userid" });

        var auditActionIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.Action),
            new CreateIndexOptions { Name = "ix_auditlog_action" });

        var auditResourceTypeIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.ResourceType),
            new CreateIndexOptions { Name = "ix_auditlog_resourcetype" });

        var auditResourceIdIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.ResourceId),
            new CreateIndexOptions { Name = "ix_auditlog_resourceid" });

        var auditCreatedAtIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys.Ascending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "ix_auditlog_createdat" });

        var auditTenantCreatedAtIndex = new CreateIndexModel<AuditLog>(
            Builders<AuditLog>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "ix_auditlog_tenantid_createdat" });

        await auditLogs.Indexes.CreateManyAsync(
            [
                auditTenantIdIndex,
                auditUserIdIndex,
                auditActionIndex,
                auditResourceTypeIndex,
                auditResourceIdIndex,
                auditCreatedAtIndex,
                auditTenantCreatedAtIndex,
            ],
            cancellationToken);

    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
