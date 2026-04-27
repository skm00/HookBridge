using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IUsageMetricRepository
{
    Task<UsageMetric> GetOrCreateCurrentMonthAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task IncrementEventsReceivedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task IncrementEventsDeliveredAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task IncrementEventsFailedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default);
}
