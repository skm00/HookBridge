using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Services;

public sealed class UsageService(
    IMongoRepository<Tenant> tenantRepository,
    IUsageMetricRepository usageMetricRepository,
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
        return usage.EventsReceived < tenant.MonthlyEventLimit;
    }
}
