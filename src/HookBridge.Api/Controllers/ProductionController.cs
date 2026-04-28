using Asp.Versioning;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.DTOs.System;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/system")]
public sealed class ProductionController(
    IProductionReadinessService productionReadinessService,
    ILogger<ProductionController> logger) : ApiControllerBase
{
    [HttpGet("production-readiness")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(typeof(ApiResponse<ProductionReadinessResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ProductionReadinessResponseDto>>> GetProductionReadinessAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Production readiness check requested.");

        var readiness = await productionReadinessService.CheckAsync(cancellationToken);
        return OkResponse(readiness);
    }
}
