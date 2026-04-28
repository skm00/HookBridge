using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HookBridge.Api.Features;

public sealed class RequireFeatureFilter(
    string featureName,
    IFeatureFlagService featureFlagService,
    ICurrentUserContext currentUserContext) : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantId = ResolveTenantId(context);
        var isEnabled = string.IsNullOrWhiteSpace(tenantId)
            ? featureFlagService.IsEnabled(featureName)
            : featureFlagService.IsEnabled(featureName, tenantId);

        if (!isEnabled)
        {
            context.Result = new NotFoundResult();
            return Task.CompletedTask;
        }

        return next();
    }

    private string? ResolveTenantId(ActionExecutingContext context)
    {
        if (context.RouteData.Values.TryGetValue("tenantId", out var routeTenantId)
            && routeTenantId is not null)
        {
            return routeTenantId.ToString();
        }

        return currentUserContext.TenantId;
    }
}
