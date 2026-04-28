using HookBridge.Api.Features;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class RequireFeatureFilterTests
{
    [Fact]
    public async Task DisabledFeature_BlocksEndpointWithNotFound()
    {
        var filter = new RequireFeatureFilter("EnableBilling", new FakeFeatureFlagService(false), new FakeCurrentUserContext());
        var context = CreateContext("tenant-1");
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        Assert.IsType<NotFoundResult>(context.Result);
    }

    [Fact]
    public async Task EnabledFeature_AllowsEndpoint()
    {
        var filter = new RequireFeatureFilter("EnableBilling", new FakeFeatureFlagService(true), new FakeCurrentUserContext());
        var context = CreateContext("tenant-1");
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], controller: new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(string tenantId)
    {
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["tenantId"] = tenantId;

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
    }

    private sealed class FakeFeatureFlagService(bool enabled) : IFeatureFlagService
    {
        public bool IsEnabled(string flagName) => enabled;

        public bool IsEnabled(string flagName, string tenantId) => enabled;
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public string? UserId => null;
        public string? TenantId => null;
        public string? Email => null;
        public string? Role => null;
        public bool IsAuthenticated => false;
    }
}
