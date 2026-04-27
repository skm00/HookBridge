using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Overview_ReturnsCurrentTenantMetrics()
    {
        var service = CreateService();

        var result = await service.GetOverviewAsync("tenant-1");

        Assert.Equal("tenant-1", result.TenantId);
        Assert.Equal("Tenant One", result.TenantName);
        Assert.Equal("Pro", result.Plan);
        Assert.Equal(50000, result.MonthlyEventLimit);
        Assert.Equal(120, result.EventsReceivedThisMonth);
        Assert.Equal(110, result.EventsDeliveredThisMonth);
        Assert.Equal(10, result.EventsFailedThisMonth);
    }

    [Fact]
    public async Task Overview_UsesCurrentMonthDateRange()
    {
        var deliveryAttempts = new FakeDeliveryAttemptRepository();
        var service = CreateService(deliveryAttemptRepository: deliveryAttempts);

        var result = await service.GetOverviewAsync("tenant-1");

        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), result.FromDate);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result.ToDate);

        Assert.All(deliveryAttempts.Calls, call =>
        {
            Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), call.FromDate);
            Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), call.ToDate);
        });
    }

    [Fact]
    public async Task SuccessRate_IsZero_WhenNoDeliveryAttempts()
    {
        var service = CreateService(
            deliveryAttemptRepository: new FakeDeliveryAttemptRepository
            {
                TotalAttempts = 0,
                SuccessfulAttempts = 0,
                FailedAttempts = 0,
            });

        var result = await service.GetOverviewAsync("tenant-1");

        Assert.Equal(0, result.SuccessRate);
    }

    [Fact]
    public async Task SuccessRate_IsCalculatedCorrectly()
    {
        var service = CreateService(
            deliveryAttemptRepository: new FakeDeliveryAttemptRepository
            {
                TotalAttempts = 3,
                SuccessfulAttempts = 2,
                FailedAttempts = 1,
            });

        var result = await service.GetOverviewAsync("tenant-1");

        Assert.Equal(66.67, result.SuccessRate);
    }

    [Fact]
    public async Task FailedEventsInDlq_CountsOnlyDlqStatus()
    {
        var failedEvents = new FakeFailedEventRepository();
        var service = CreateService(failedEventRepository: failedEvents);

        var result = await service.GetOverviewAsync("tenant-1");

        Assert.Equal(7, result.FailedEventsInDlq);
        Assert.Equal("DLQ", failedEvents.LastStatus);
    }

    private static DashboardService CreateService(
        IMongoRepository<Tenant>? tenantRepository = null,
        IUsageMetricRepository? usageMetricRepository = null,
        FakeDeliveryAttemptRepository? deliveryAttemptRepository = null,
        FakeFailedEventRepository? failedEventRepository = null)
    {
        return new DashboardService(
            tenantRepository ?? new FakeTenantRepository(),
            usageMetricRepository ?? new FakeUsageMetricRepository(),
            deliveryAttemptRepository ?? new FakeDeliveryAttemptRepository(),
            failedEventRepository ?? new FakeFailedEventRepository(),
            new FixedDateTimeProvider(),
            NullLogger<DashboardService>.Instance);
    }

    private sealed class FakeTenantRepository : IMongoRepository<Tenant>
    {
        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<Tenant?>(new Tenant { Id = id, Name = "Tenant One", Plan = BillingPlan.Pro, MonthlyEventLimit = 50000 });

        public Task<IReadOnlyList<Tenant>> FindAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, MongoDB.Driver.SortDefinition<Tenant> sort, int skip, int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Tenant?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeUsageMetricRepository : IUsageMetricRepository
    {
        public Task<UsageMetric> GetOrCreateCurrentMonthAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsageMetric
            {
                TenantId = tenantId,
                Year = year,
                Month = month,
                EventsReceived = 120,
                EventsDelivered = 110,
                EventsFailed = 10,
                LastUpdatedAt = nowUtc,
            });

        public Task IncrementEventsReceivedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task IncrementEventsDeliveredAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task IncrementEventsFailedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDeliveryAttemptRepository : IDeliveryAttemptRepository
    {
        public long TotalAttempts { get; set; } = 200;

        public long SuccessfulAttempts { get; set; } = 180;

        public long FailedAttempts { get; set; } = 20;

        public List<(string TenantId, DateTime FromDate, DateTime ToDate, DeliveryStatus? Status)> Calls { get; } = [];

        public Task<(IReadOnlyList<DeliveryAttempt> Items, long TotalCount)> SearchAsync(
            HookBridge.Application.DTOs.DeliveryAttempts.DeliveryAttemptSearchRequestDto request,
            MongoDB.Driver.SortDefinition<DeliveryAttempt> sort,
            int skip,
            int limit,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountAsync(string tenantId, DateTime fromDateInclusive, DateTime toDateExclusive, DeliveryStatus? status, CancellationToken cancellationToken = default)
        {
            Calls.Add((tenantId, fromDateInclusive, toDateExclusive, status));

            return status switch
            {
                DeliveryStatus.Success => Task.FromResult(SuccessfulAttempts),
                DeliveryStatus.Failed => Task.FromResult(FailedAttempts),
                _ => Task.FromResult(TotalAttempts),
            };
        }
    }

    private sealed class FakeFailedEventRepository : IFailedEventRepository
    {
        public string? LastStatus { get; private set; }

        public Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<FailedEvent> Items, long TotalCount)> SearchAsync(HookBridge.Application.DTOs.FailedEvents.FailedEventSearchRequestDto request, MongoDB.Driver.SortDefinition<FailedEvent> sort, int skip, int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountByStatusAsync(string tenantId, string status, CancellationToken cancellationToken = default)
        {
            LastStatus = status;
            return Task.FromResult(status == "DLQ" ? 7L : 0L);
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc);
    }
}
