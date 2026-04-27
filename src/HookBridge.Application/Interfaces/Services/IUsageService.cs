using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Services;

public interface IUsageService
{
    Task<UsageMetric> GetCurrentMonthUsageAsync(string tenantId, CancellationToken cancellationToken = default);

    Task IncrementEventsReceivedAsync(string tenantId, CancellationToken cancellationToken = default);

    Task IncrementEventsDeliveredAsync(string tenantId, CancellationToken cancellationToken = default);

    Task IncrementEventsFailedAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<bool> CanAcceptEventAsync(string tenantId, CancellationToken cancellationToken = default);
}
