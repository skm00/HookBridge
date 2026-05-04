using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.Dashboard;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class DashboardControllerTests
{
    private static T WithHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetOverview_ReturnsCurrentTenantMetrics()
    {
        var service = new FakeDashboardService();
        var controller = WithHttpContext(new DashboardController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" }));

        var result = await controller.GetOverviewAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DashboardOverviewResponseDto>(ok.Value);

        Assert.Equal("tenant-1", service.LastTenantId);
        Assert.Equal("tenant-1", payload.TenantId);
    }

    [Fact]
    public async Task GetOverview_ReturnsOnlyCurrentTenantData()
    {
        var service = new FakeDashboardService();
        var controller = WithHttpContext(new DashboardController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-a" }));

        var result = await controller.GetOverviewAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DashboardOverviewResponseDto>(ok.Value);

        Assert.Equal("tenant-a", service.LastTenantId);
        Assert.Equal("tenant-a", payload.TenantId);
    }

    private sealed class FakeDashboardService : IDashboardService
    {
        public string? LastTenantId { get; private set; }

        public Task<DashboardOverviewResponseDto> GetOverviewAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            return Task.FromResult(new DashboardOverviewResponseDto
            {
                TenantId = tenantId,
                TenantName = "Tenant",
                Plan = "Free",
            });
        }
    }
}
