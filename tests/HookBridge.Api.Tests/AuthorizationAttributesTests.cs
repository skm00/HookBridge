using HookBridge.Api.Controllers;
using HookBridge.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class AuthorizationAttributesTests
{
    [Theory]
    [InlineData(typeof(TenantsController))]
    [InlineData(typeof(ApiKeysController))]
    [InlineData(typeof(SubscriptionsController))]
    [InlineData(typeof(DeliveryLogsController))]
    [InlineData(typeof(FailedEventsController))]
    [InlineData(typeof(UsageController))]
    [InlineData(typeof(BillingController))]
    [InlineData(typeof(DashboardController))]
    public void AdminEndpoints_RequireAuthorization(Type controllerType)
    {
        var authorize = Attribute.GetCustomAttribute(controllerType, typeof(AuthorizeAttribute));
        Assert.NotNull(authorize);
    }

    [Fact]
    public void EventIngestionEndpoint_DoesNotRequireAuthorization()
    {
        var authorize = Attribute.GetCustomAttribute(typeof(EventsController), typeof(AuthorizeAttribute));
        Assert.Null(authorize);
    }

    [Fact]
    public void Sensitive_AdminEndpoints_HaveExpectedPolicies()
    {
        AssertMethodPolicy<TenantsController>(nameof(TenantsController.DisableAsync), AuthorizationPolicies.OwnerOnly);
        AssertMethodPolicy<BillingController>(nameof(BillingController.CreateCheckoutAsync), AuthorizationPolicies.OwnerOnly);
        AssertMethodPolicy<ApiKeysController>(nameof(ApiKeysController.RevokeAsync), AuthorizationPolicies.OwnerOnly);
        AssertMethodPolicy<FailedEventsController>(nameof(FailedEventsController.RetryAsync), AuthorizationPolicies.AdminOrOwner);
        AssertMethodPolicy<DashboardController>(nameof(DashboardController.GetOverviewAsync), AuthorizationPolicies.ViewerOrAbove);
    }

    private static void AssertMethodPolicy<TController>(string methodName, string expectedPolicy)
    {
        var method = typeof(TController).GetMethod(methodName);
        Assert.NotNull(method);

        var authorize = Attribute.GetCustomAttribute(method!, typeof(AuthorizeAttribute)) as AuthorizeAttribute;
        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
    }
}
