using HookBridge.Api.Controllers;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.DTOs.Usage;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Xunit;
using MongoDB.Driver;

namespace HookBridge.Api.Tests;

public sealed class UsageControllerTests
{
    private static T WithHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetCurrent_ReturnsCurrentMonthUsage()
    {
        var controller = WithHttpContext(new UsageController(
            new FakeUsageService(),
            new FakeTenantRepository(),
            TenantIsolationTestHelpers.CreateValidator()));

        var result = await controller.GetCurrentAsync("tenant-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HookBridge.Shared.Api.ApiResponse<CurrentUsageResponseDto>>(ok.Value);
        var payload = Assert.IsType<CurrentUsageResponseDto>(response.Data);
        Assert.Equal("tenant-1", payload.TenantId);
        Assert.Equal(2026, payload.Year);
        Assert.Equal(4, payload.Month);
        Assert.Equal(1000, payload.MonthlyEventLimit);
        Assert.Equal(BillingPlan.Free, payload.Plan);
    }

    private sealed class FakeUsageService : IUsageService
    {
        public Task<UsageMetric> GetCurrentMonthUsageAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsageMetric
            {
                TenantId = tenantId,
                Year = 2026,
                Month = 4,
                EventsReceived = 100,
                EventsDelivered = 90,
                EventsFailed = 5,
                LastUpdatedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc),
            });

        public Task IncrementEventsReceivedAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task IncrementEventsDeliveredAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task IncrementEventsFailedAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> CanAcceptEventAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeTenantRepository : IMongoRepository<Tenant>
    {
        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<Tenant?>(new Tenant { Id = id, Plan = BillingPlan.Free, MonthlyEventLimit = 1000 });

        public Task<IReadOnlyList<Tenant>> FindAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Tenant?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, SortDefinition<Tenant> sort, int skip, int limit, CancellationToken cancellationToken = default)
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
}
