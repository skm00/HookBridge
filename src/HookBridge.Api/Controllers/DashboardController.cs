using Asp.Versioning;
using HookBridge.Api.Authorization;
using HookBridge.Api.Features;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.DTOs.Dashboard;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[RequireFeature("EnableAdvancedDashboard")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentUserContext currentUserContext) : ApiControllerBase
{
    [HttpGet("overview")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<DashboardOverviewResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DashboardOverviewResponseDto>>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUserContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new UnauthorizedException("Tenant context is missing.");
        }

        var overview = await dashboardService.GetOverviewAsync(tenantId, cancellationToken);
        return OkResponse(overview);
    }
}
