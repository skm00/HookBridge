using HookBridge.Application.DTOs.Dashboard;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class DashboardService(
    IMongoRepository<Tenant> tenantRepository,
    IUsageMetricRepository usageMetricRepository,
    IDeliveryAttemptRepository deliveryAttemptRepository,
    IFailedEventRepository failedEventRepository,
    IDateTimeProvider dateTimeProvider,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardOverviewResponseDto> GetOverviewAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");

        var nowUtc = dateTimeProvider.UtcNow;
        var fromDate = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = fromDate.AddMonths(1);

        logger.LogInformation(
            "Loading dashboard overview metrics for tenant {TenantId} from {FromDate} to {ToDate}",
            tenantId,
            fromDate,
            toDate);

        var usageMetric = await usageMetricRepository.GetOrCreateCurrentMonthAsync(
            tenantId,
            nowUtc.Year,
            nowUtc.Month,
            nowUtc,
            cancellationToken);

        var totalAttempts = await deliveryAttemptRepository.CountAsync(
            tenantId,
            fromDate,
            toDate,
            status: null,
            cancellationToken);

        var successfulAttempts = await deliveryAttemptRepository.CountAsync(
            tenantId,
            fromDate,
            toDate,
            DeliveryStatus.Success,
            cancellationToken);

        var failedAttempts = await deliveryAttemptRepository.CountAsync(
            tenantId,
            fromDate,
            toDate,
            DeliveryStatus.Failed,
            cancellationToken);

        var failedEventsInDlq = await failedEventRepository.CountByStatusAsync(
            tenantId,
            "DLQ",
            cancellationToken);

        var successRate = totalAttempts == 0
            ? 0
            : Math.Round((double)successfulAttempts / totalAttempts * 100, 2, MidpointRounding.AwayFromZero);

        return new DashboardOverviewResponseDto
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Plan = tenant.Plan.ToString(),
            MonthlyEventLimit = tenant.MonthlyEventLimit,
            EventsReceivedThisMonth = usageMetric.EventsReceived,
            EventsDeliveredThisMonth = usageMetric.EventsDelivered,
            EventsFailedThisMonth = usageMetric.EventsFailed,
            TotalDeliveryAttemptsThisMonth = totalAttempts,
            SuccessfulDeliveryAttemptsThisMonth = successfulAttempts,
            FailedDeliveryAttemptsThisMonth = failedAttempts,
            FailedEventsInDlq = failedEventsInDlq,
            SuccessRate = successRate,
            FromDate = fromDate,
            ToDate = toDate,
        };
    }
}
