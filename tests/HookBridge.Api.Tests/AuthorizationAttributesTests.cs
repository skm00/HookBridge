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
    [InlineData(typeof(IncomingEventsController))]
    [InlineData(typeof(BillingController))]
    [InlineData(typeof(DashboardController))]
    [InlineData(typeof(NotificationsController))]
    [InlineData(typeof(BackupController))]
    [InlineData(typeof(AiRecommendationApprovalsController))]
    [InlineData(typeof(AdminAiActionsController))]
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
        AssertMethodPolicy<NotificationsController>(nameof(NotificationsController.SearchAsync), AuthorizationPolicies.ViewerOrAbove);
        AssertMethodPolicy<IncomingEventsController>(nameof(IncomingEventsController.SearchAsync), AuthorizationPolicies.DeveloperOrAbove);
        AssertMethodPolicy<BackupController>(nameof(BackupController.ExportAsync), AuthorizationPolicies.OwnerOnly);
        AssertMethodPolicy<BackupController>(nameof(BackupController.ImportAsync), AuthorizationPolicies.OwnerOnly);
        AssertMethodPolicy<AiRecommendationApprovalsController>(nameof(AiRecommendationApprovalsController.SearchAsync), AuthorizationPolicies.ViewerOrAbove);
        AssertMethodPolicy<AiRecommendationApprovalsController>(nameof(AiRecommendationApprovalsController.GetPendingAsync), AuthorizationPolicies.ViewerOrAbove);
        AssertMethodPolicy<AiRecommendationApprovalsController>(nameof(AiRecommendationApprovalsController.GetByIdAsync), AuthorizationPolicies.ViewerOrAbove);
        AssertMethodPolicy<AiRecommendationApprovalsController>(nameof(AiRecommendationApprovalsController.CreateAsync), AuthorizationPolicies.AdminOrOwner);
        AssertMethodPolicy<AiRecommendationApprovalsController>(nameof(AiRecommendationApprovalsController.UpdateStatusAsync), AuthorizationPolicies.AdminOrOwner);
        AssertControllerPolicy<AdminAiActionsController>(AuthorizationPolicies.AdminOrOwner);
    }

    private static void AssertControllerPolicy<TController>(string expectedPolicy)
    {
        var authorize = Attribute.GetCustomAttribute(typeof(TController), typeof(AuthorizeAttribute)) as AuthorizeAttribute;
        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
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
