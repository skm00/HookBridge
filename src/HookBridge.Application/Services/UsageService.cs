using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Services;

public sealed class UsageService(
    IMongoRepository<Tenant> tenantRepository,
    IUsageMetricRepository usageMetricRepository,
    INotificationService notificationService,
    INotificationRepository notificationRepository,
    IDateTimeProvider dateTimeProvider) : IUsageService
{
    public Task<UsageMetric> GetCurrentMonthUsageAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        return usageMetricRepository.GetOrCreateCurrentMonthAsync(tenantId, now.Year, now.Month, now, cancellationToken);
    }

    public async Task IncrementEventsReceivedAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        await usageMetricRepository.IncrementEventsReceivedAsync(tenantId, now.Year, now.Month, now, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");

        if (tenant.Plan == BillingPlan.Enterprise || tenant.MonthlyEventLimit <= 0)
        {
            return;
        }

        var usage = await GetCurrentMonthUsageAsync(tenantId, cancellationToken);
        var warningThreshold = (int)Math.Ceiling(tenant.MonthlyEventLimit * 0.8m);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        if (usage.EventsReceived >= warningThreshold && usage.EventsReceived < tenant.MonthlyEventLimit)
        {
            var warningExists = await notificationRepository.ExistsAsync(
                tenantId,
                NotificationTypes.UsageLimitWarning,
                monthStart,
                monthEnd,
                cancellationToken);

            if (!warningExists)
            {
                await notificationService.CreateAsync(new Notification
                {
                    TenantId = tenantId,
                    Type = NotificationTypes.UsageLimitWarning,
                    Severity = NotificationSeverities.Warning,
                    Title = "Usage limit warning",
                    Message = $"You have reached {usage.EventsReceived} of {tenant.MonthlyEventLimit} events this month.",
                    IsRead = false,
                }, cancellationToken);
            }
        }

        if (usage.EventsReceived >= tenant.MonthlyEventLimit)
        {
            await CreateUsageExceededNotificationIfMissingAsync(tenantId, monthStart, monthEnd, usage.EventsReceived, tenant.MonthlyEventLimit, cancellationToken);
        }
    }

    public async Task IncrementEventsDeliveredAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        await usageMetricRepository.IncrementEventsDeliveredAsync(tenantId, now.Year, now.Month, now, cancellationToken);
    }

    public async Task IncrementEventsFailedAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        await usageMetricRepository.IncrementEventsFailedAsync(tenantId, now.Year, now.Month, now, cancellationToken);
    }

    public async Task<bool> CanAcceptEventAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");

        if (tenant.Plan == BillingPlan.Enterprise)
        {
            return true;
        }

        var usage = await GetCurrentMonthUsageAsync(tenantId, cancellationToken);
        if (usage.EventsReceived < tenant.MonthlyEventLimit)
        {
            return true;
        }

        var now = dateTimeProvider.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        await CreateUsageExceededNotificationIfMissingAsync(tenantId, monthStart, monthEnd, usage.EventsReceived, tenant.MonthlyEventLimit, cancellationToken);
        return false;
    }

    private async Task CreateUsageExceededNotificationIfMissingAsync(
        string tenantId,
        DateTime monthStart,
        DateTime monthEnd,
        long eventsReceived,
        int monthlyLimit,
        CancellationToken cancellationToken)
    {
        var exceededExists = await notificationRepository.ExistsAsync(
            tenantId,
            NotificationTypes.UsageLimitExceeded,
            monthStart,
            monthEnd,
            cancellationToken);

        if (exceededExists)
        {
            return;
        }

        await notificationService.CreateAsync(new Notification
        {
            TenantId = tenantId,
            Type = NotificationTypes.UsageLimitExceeded,
            Severity = NotificationSeverities.Critical,
            Title = "Usage limit exceeded",
            Message = $"Monthly usage limit exceeded ({eventsReceived}/{monthlyLimit}).",
            IsRead = false,
        }, cancellationToken);
    }
}
