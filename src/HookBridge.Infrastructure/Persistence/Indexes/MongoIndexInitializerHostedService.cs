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

    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
