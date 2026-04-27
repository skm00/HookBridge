using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.DTOs.Dashboard;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v1/admin/dashboard")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet("overview")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(DashboardOverviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardOverviewResponseDto>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentUserContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new UnauthorizedException("Tenant context is missing.");
        }

        var overview = await dashboardService.GetOverviewAsync(tenantId, cancellationToken);
        return Ok(overview);
    }
}
