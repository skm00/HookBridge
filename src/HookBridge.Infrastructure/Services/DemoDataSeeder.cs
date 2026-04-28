using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Services;

public sealed class DemoDataSeeder(
    IMongoRepository<Tenant> tenantRepository,
    IMongoRepository<AdminUser> adminUserRepository,
    IMongoRepository<ApiKey> apiKeyRepository,
    IMongoRepository<Subscription> subscriptionRepository,
    IMongoRepository<IncomingEvent> incomingEventRepository,
    IMongoRepository<DeliveryAttempt> deliveryAttemptRepository,
    IMongoRepository<FailedEvent> failedEventRepository,
    IMongoRepository<Notification> notificationRepository,
    IMongoRepository<AuditLog> auditLogRepository,
    IPasswordHasher passwordHasher,
    IApiKeyHasher apiKeyHasher,
    IDateTimeProvider dateTimeProvider,
    IGuidGenerator guidGenerator,
    IHostEnvironment hostEnvironment,
    IOptions<DemoDataSettings> demoDataOptions,
    ILogger<DemoDataSeeder> logger) : IDemoDataSeeder
{
    private const string DemoApiKeyName = "Demo API Key";
    private const string DemoPlainApiKey = "hb_live_demo_key_for_local_testing";
    private readonly DemoDataSettings _settings = demoDataOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("Demo data seeding skipped because DemoData:Enabled is false.");
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var demoTenant = await EnsureTenantAsync(now, cancellationToken);
        var adminUser = await EnsureAdminUserAsync(demoTenant, now, cancellationToken);
        var apiKey = await EnsureApiKeyAsync(demoTenant, now, cancellationToken);

        await EnsureSubscriptionsAsync(demoTenant, now, cancellationToken);
        await EnsureIncomingEventsAsync(demoTenant, apiKey, now, cancellationToken);
        await EnsureDeliveryAttemptsAsync(demoTenant, now, cancellationToken);
        await EnsureFailedEventsAsync(demoTenant, now, cancellationToken);
        await EnsureNotificationsAsync(demoTenant, now, cancellationToken);
        await EnsureAuditLogsAsync(demoTenant, adminUser, now, cancellationToken);
    }

    private async Task<Tenant> EnsureTenantAsync(DateTime now, CancellationToken cancellationToken)
    {
        var existing = await tenantRepository.FirstOrDefaultAsync(
            x => x.Slug == _settings.TenantSlug || x.Name == _settings.TenantName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var tenant = new Tenant
        {
            Id = guidGenerator.NewGuid(),
            Name = _settings.TenantName,
            Slug = _settings.TenantSlug,
            Plan = BillingPlan.Pro,
            MonthlyEventLimit = 500000,
            Status = TenantStatus.Active,
            BillingStatus = "Active",
            ContactEmail = _settings.AdminEmail,
            CreatedAt = now,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        return tenant;
    }

    private async Task<AdminUser> EnsureAdminUserAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        var normalizedEmail = _settings.AdminEmail.Trim().ToLowerInvariant();
        var existing = await adminUserRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.Email == normalizedEmail,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var admin = new AdminUser
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenant.Id,
            Email = normalizedEmail,
            FullName = "Demo Owner",
            Role = AdminRole.Owner,
            IsActive = true,
            PasswordHash = passwordHasher.HashPassword(_settings.AdminPassword),
            LastLoginAt = now,
            CreatedAt = now,
        };

        await adminUserRepository.AddAsync(admin, cancellationToken);
        return admin;
    }

    private async Task<ApiKey> EnsureApiKeyAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await apiKeyRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.Name == DemoApiKeyName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var apiKey = new ApiKey
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenant.Id,
            Name = DemoApiKeyName,
            KeyHash = apiKeyHasher.Hash(DemoPlainApiKey),
            KeyPrefix = "hb_live_demo****",
            IsActive = true,
            CreatedAt = now,
        };

        await apiKeyRepository.AddAsync(apiKey, cancellationToken);

        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation("Demo API key (development only): {DemoApiKey}", DemoPlainApiKey);
        }

        return apiKey;
    }

    private async Task EnsureSubscriptionsAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        await EnsureSubscriptionAsync(tenant.Id, "order.created", "https://webhook.site/demo-order-created", now, cancellationToken);
        await EnsureSubscriptionAsync(tenant.Id, "order.updated", "https://webhook.site/demo-order-updated", now, cancellationToken);
        await EnsureSubscriptionAsync(tenant.Id, "payment.failed", "https://webhook.site/demo-payment-failed", now, cancellationToken);
    }

    private async Task EnsureSubscriptionAsync(string tenantId, string eventType, string targetUrl, DateTime now, CancellationToken cancellationToken)
    {
        var existing = await subscriptionRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.EventType == eventType && x.TargetUrl == targetUrl,
            cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await subscriptionRepository.AddAsync(new Subscription
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            TargetUrl = targetUrl,
            TimeoutSeconds = 30,
            IsActive = true,
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialDelaySeconds = 10,
                BackoffType = "Exponential",
            },
            CreatedAt = now,
        }, cancellationToken);
    }

    private async Task EnsureIncomingEventsAsync(Tenant tenant, ApiKey apiKey, DateTime now, CancellationToken cancellationToken)
    {
        var specs = new (string Status, int Count)[]
        {
            ("Accepted", 10),
            ("Delivered", 20),
            ("Failed", 5),
            ("PartiallyFailed", 3),
            ("NoSubscriptions", 2),
        };

        foreach (var spec in specs)
        {
            for (var i = 1; i <= spec.Count; i++)
            {
                var eventId = $"demo-{spec.Status.ToLowerInvariant()}-{i:00}";
                var existing = await incomingEventRepository.FirstOrDefaultAsync(
                    x => x.TenantId == tenant.Id && x.EventId == eventId,
                    cancellationToken);

                if (existing is not null)
                {
                    continue;
                }

                await incomingEventRepository.AddAsync(new IncomingEvent
                {
                    Id = guidGenerator.NewGuid(),
                    TenantId = tenant.Id,
                    EventId = eventId,
                    EventType = i % 3 == 0 ? "payment.failed" : i % 2 == 0 ? "order.updated" : "order.created",
                    Payload = new { demo = true, status = spec.Status, index = i },
                    Status = spec.Status,
                    ApiKeyId = apiKey.Id,
                    CorrelationId = $"corr-{eventId}",
                    ReceivedAt = now.AddMinutes(-(i * 3)),
                    CreatedAt = now.AddMinutes(-(i * 3)),
                }, cancellationToken);
            }
        }
    }

    private async Task EnsureDeliveryAttemptsAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.FindAsync(x => x.TenantId == tenant.Id, cancellationToken);
        var incomingEvents = await incomingEventRepository.FindAsync(
            x => x.TenantId == tenant.Id && (x.Status == "Delivered" || x.Status == "Failed" || x.Status == "PartiallyFailed"),
            cancellationToken);

        if (subscriptions.Count == 0 || incomingEvents.Count == 0)
        {
            return;
        }

        var statusCodes = new[] { 200, 201, 400, 401, 500, 503 };
        var durations = new long[] { 24, 61, 112, 220, 515, 889 };

        for (var i = 0; i < 24; i++)
        {
            var selectedEvent = incomingEvents[i % incomingEvents.Count];
            var subscription = subscriptions[i % subscriptions.Count];
            var correlationId = $"corr-{selectedEvent.EventId}-{subscription.Id}";

            var existing = await deliveryAttemptRepository.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.CorrelationId == correlationId,
                cancellationToken);

            if (existing is not null)
            {
                continue;
            }

            var httpStatus = statusCodes[i % statusCodes.Length];
            var status = httpStatus is 200 or 201 ? DeliveryStatus.Success : DeliveryStatus.Failed;

            await deliveryAttemptRepository.AddAsync(new DeliveryAttempt
            {
                Id = guidGenerator.NewGuid(),
                TenantId = tenant.Id,
                EventId = selectedEvent.Id,
                EventType = selectedEvent.EventType,
                SubscriptionId = subscription.Id,
                TargetUrl = subscription.TargetUrl,
                AttemptNumber = (i % 3) + 1,
                Status = status,
                HttpStatusCode = httpStatus,
                ResponseBody = status == DeliveryStatus.Success ? "{\"ok\":true}" : "{\"error\":\"demo failure\"}",
                ResponseBodyTruncated = false,
                ErrorMessage = status == DeliveryStatus.Success ? null : $"Demo failure with status {httpStatus}",
                DurationMs = durations[i % durations.Length],
                AttemptedAt = now.AddMinutes(-(i + 1)),
                CorrelationId = correlationId,
                CreatedAt = now.AddMinutes(-(i + 1)),
            }, cancellationToken);
        }
    }

    private async Task EnsureFailedEventsAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.FindAsync(x => x.TenantId == tenant.Id, cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        for (var i = 1; i <= 3; i++)
        {
            await EnsureFailedEventAsync(
                tenant.Id,
                $"demo-dlq-{i:00}",
                subscriptions[(i - 1) % subscriptions.Count],
                "DLQ",
                "Moved to dead letter queue after retries.",
                503,
                now.AddMinutes(-(20 + i)),
                cancellationToken);
        }

        await EnsureFailedEventAsync(
            tenant.Id,
            "demo-retry-requested-01",
            subscriptions[0],
            "RetryRequested",
            "Manual retry requested by demo admin.",
            500,
            now.AddMinutes(-3),
            cancellationToken);
    }

    private async Task EnsureFailedEventAsync(
        string tenantId,
        string eventId,
        Subscription subscription,
        string status,
        string reason,
        int httpStatus,
        DateTime failedAt,
        CancellationToken cancellationToken)
    {
        var existing = await failedEventRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.EventId == eventId && x.SubscriptionId == subscription.Id,
            cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await failedEventRepository.AddAsync(new FailedEvent
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            SubscriptionId = subscription.Id,
            EventType = subscription.EventType,
            TargetUrl = subscription.TargetUrl,
            Reason = reason,
            FinalAttemptNumber = 5,
            LastHttpStatusCode = httpStatus,
            LastErrorMessage = reason,
            Status = status,
            FailedAt = failedAt,
            CorrelationId = $"corr-{eventId}",
            CreatedAt = failedAt,
        }, cancellationToken);
    }

    private async Task EnsureNotificationsAsync(Tenant tenant, DateTime now, CancellationToken cancellationToken)
    {
        await EnsureNotificationAsync(tenant.Id, NotificationTypes.UsageLimitWarning, NotificationSeverities.Warning, "Usage limit warning", now.AddMinutes(-30), cancellationToken);
        await EnsureNotificationAsync(tenant.Id, NotificationTypes.DlqCreated, NotificationSeverities.Error, "DLQ event created", now.AddMinutes(-20), cancellationToken);
        await EnsureNotificationAsync(tenant.Id, NotificationTypes.BillingPaymentFailed, NotificationSeverities.Critical, "Billing payment failed", now.AddMinutes(-10), cancellationToken);
    }

    private async Task EnsureNotificationAsync(
        string tenantId,
        string type,
        string severity,
        string title,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var existing = await notificationRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Type == type,
            cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await notificationRepository.AddAsync(new Notification
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            Type = type,
            Severity = severity,
            Title = title,
            Message = $"Demo notification for {type}.",
            IsRead = false,
            CreatedAt = createdAt,
        }, cancellationToken);
    }

    private async Task EnsureAuditLogsAsync(Tenant tenant, AdminUser adminUser, DateTime now, CancellationToken cancellationToken)
    {
        await EnsureAuditLogAsync(tenant.Id, adminUser, "AdminLogin", "AdminUser", adminUser.Id, "Demo admin signed in.", now.AddMinutes(-45), cancellationToken);
        await EnsureAuditLogAsync(tenant.Id, adminUser, "SubscriptionCreated", "Subscription", "demo-subscription-created", "Demo subscription created.", now.AddMinutes(-35), cancellationToken);
        await EnsureAuditLogAsync(tenant.Id, adminUser, "ApiKeyCreated", "ApiKey", "demo-api-key-created", "Demo API key created.", now.AddMinutes(-25), cancellationToken);
        await EnsureAuditLogAsync(tenant.Id, adminUser, "ManualRetryRequested", "FailedEvent", "demo-retry-requested-01", "Manual retry requested.", now.AddMinutes(-15), cancellationToken);
        await EnsureAuditLogAsync(tenant.Id, adminUser, "BillingCheckoutCreated", "Billing", "demo-billing-checkout", "Billing checkout session created.", now.AddMinutes(-5), cancellationToken);
    }

    private async Task EnsureAuditLogAsync(
        string tenantId,
        AdminUser adminUser,
        string action,
        string resourceType,
        string resourceId,
        string description,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var existing = await auditLogRepository.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Action == action,
            cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await auditLogRepository.AddAsync(new AuditLog
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenantId,
            UserId = adminUser.Id,
            UserEmail = adminUser.Email,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Description = description,
            Metadata = new Dictionary<string, object?> { ["source"] = "demo-seed" },
            IpAddress = "127.0.0.1",
            UserAgent = "HookBridge Demo Seeder",
            CreatedAt = createdAt,
        }, cancellationToken);
    }
}
