using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using Microsoft.AspNetCore.Routing;

namespace HookBridge.Api.Security;

public sealed class TenantAccessValidator(
    ICurrentUserContext currentUserContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TenantAccessValidator> logger)
{
    public void EnsureTenantAccess(string requestedTenantId)
    {
        if (!currentUserContext.IsAuthenticated)
        {
            throw new UnauthorizedException("Authenticated user is required.");
        }

        if (string.IsNullOrWhiteSpace(currentUserContext.TenantId))
        {
            throw new UnauthorizedException("Authenticated user tenant is required.");
        }

        if (string.Equals(requestedTenantId, currentUserContext.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var routeData = httpContextAccessor.HttpContext?.GetRouteData();
        logger.LogWarning(
            "Blocked cross-tenant access. CurrentTenantId: {CurrentTenantId}, RequestedTenantId: {RequestedTenantId}, UserId: {UserId}, Controller: {Controller}, Action: {Action}",
            currentUserContext.TenantId,
            requestedTenantId,
            currentUserContext.UserId,
            routeData?.Values["controller"],
            routeData?.Values["action"]);

        throw new ForbiddenException("Cross-tenant access is forbidden.");
    }
}
