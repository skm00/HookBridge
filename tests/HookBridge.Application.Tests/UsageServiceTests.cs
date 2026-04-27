using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
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
        var notifications = new RecordingNotificationService();
        var service = new UsageService(tenantRepository, usageRepository, notifications, notifications, new FixedDateTimeProvider());

        var canAccept = await service.CanAcceptEventAsync("tenant-1");

        Assert.True(canAccept);
    }

    [Fact]
    public async Task CanAcceptEvent_ReturnsFalse_AtLimit_AndCreatesExceededNotificationOncePerMonth()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1000, Plan = BillingPlan.Free });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 1000 };
        var notifications = new RecordingNotificationService();
        var service = new UsageService(tenantRepository, usageRepository, notifications, notifications, new FixedDateTimeProvider());

        var first = await service.CanAcceptEventAsync("tenant-1");
        var second = await service.CanAcceptEventAsync("tenant-1");

        Assert.False(first);
        Assert.False(second);
        Assert.Single(notifications.Created.Where(x => x.Type == "UsageLimitExceeded"));
    }

    [Fact]
    public async Task EnterprisePlan_HasUnlimitedEvents()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1, Plan = BillingPlan.Enterprise });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 100000 };
        var notifications = new RecordingNotificationService();
        var service = new UsageService(tenantRepository, usageRepository, notifications, notifications, new FixedDateTimeProvider());

        var canAccept = await service.CanAcceptEventAsync("tenant-1");

        Assert.True(canAccept);
    }

    [Fact]
    public async Task IncrementEventsReceived_CreatesUsageWarningOncePerMonth()
    {
        var tenantRepository = new InMemoryTenantRepository(new Tenant { Id = "tenant-1", MonthlyEventLimit = 1000, Plan = BillingPlan.Free });
        var usageRepository = new FakeUsageMetricRepository { EventsReceived = 799 };
        var notifications = new RecordingNotificationService();
        var service = new UsageService(tenantRepository, usageRepository, notifications, notifications, new FixedDateTimeProvider());

        await service.IncrementEventsReceivedAsync("tenant-1");
        await service.IncrementEventsReceivedAsync("tenant-1");

        Assert.Single(notifications.Created.Where(x => x.Type == "UsageLimitWarning"));
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
        {
            EventsReceived++;
            return Task.CompletedTask;
        }

        public Task IncrementEventsDeliveredAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task IncrementEventsFailedAsync(string tenantId, int year, int month, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingNotificationService : INotificationService, INotificationRepository
    {
        public List<Notification> Created { get; } = [];

        public Task CreateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            notification.CreatedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc);
            Created.Add(notification);
            return Task.CompletedTask;
        }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.Notifications.NotificationResponseDto>> SearchAsync(HookBridge.Application.DTOs.Notifications.NotificationSearchRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<HookBridge.Application.DTOs.Notifications.NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<Notification> Items, long TotalCount)> SearchAsync(HookBridge.Application.DTOs.Notifications.NotificationSearchRequestDto request, MongoDB.Driver.SortDefinition<Notification> sort, int skip, int limit, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        Task<Notification?> INotificationRepository.GetByIdAsync(string id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        Task<int> INotificationRepository.GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string tenantId, string type, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken = default)
            => Task.FromResult(Created.Any(x => x.TenantId == tenantId && x.Type == type && x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive));
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc);
    }
}
