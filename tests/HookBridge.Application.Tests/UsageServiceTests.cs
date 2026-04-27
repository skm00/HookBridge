using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class UsageServiceTests
{
    [Fact]
    public void FreePlan_DefaultLimit_Is1000()
    {
        var tenant = new Tenant();
        Assert.Equal(BillingPlan.Free, tenant.Plan);
        Assert.Equal(1000, tenant.MonthlyEventLimit);
    }

    [Fact]
    public async Task CanAcceptEvent_ReturnsTrue_UnderLimit()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1000, Plan = BillingPlan.Free });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 999 };
        var service = new UsageService(tenantRepository, usageRepository, new FixedDateTimeProvider());

        var canAccept = await service.CanAcceptEventAsync("tenant-1");

        Assert.True(canAccept);
    }

    [Fact]
    public async Task CanAcceptEvent_ReturnsFalse_AtLimit()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1000, Plan = BillingPlan.Free });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 1000 };
        var service = new UsageService(tenantRepository, usageRepository, new FixedDateTimeProvider());

        var canAccept = await service.CanAcceptEventAsync("tenant-1");

        Assert.False(canAccept);
    }

    [Fact]
    public async Task EnterprisePlan_HasUnlimitedEvents()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1, Plan = BillingPlan.Enterprise });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 100000 };
        var service = new UsageService(tenantRepository, usageRepository, new FixedDateTimeProvider());

        var canAccept = await service.CanAcceptEventAsync("tenant-1");

        Assert.True(canAccept);
    }

    private sealed class InMemoryTenantRepository(Tenant tenant) : IMongoRepository<Tenant>
    {
        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<Tenant?>(tenant.Id == id ? tenant : null);

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
        public long EventsReceived { get; set; }

        public Task<UsageMetric> GetOrCreateCurrentMonthAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsageMetric { TenantId = tenantId, Year = year, Month = month, EventsReceived = EventsReceived, LastUpdatedAt = nowUtc });

        public Task IncrementEventsReceivedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task IncrementEventsDeliveredAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task IncrementEventsFailedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc);
    }
}
