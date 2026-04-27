using HookBridge.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

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
}
