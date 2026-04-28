using Asp.Versioning;
using HookBridge.Application.DTOs.Usage;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/tenants/{tenantId}/usage")]
public sealed class UsageController(
    IUsageService usageService,
    IMongoRepository<Tenant> tenantRepository,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpGet("current")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUsageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CurrentUsageResponseDto>>> GetCurrentAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return ErrorResponse<CurrentUsageResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        var usage = await usageService.GetCurrentMonthUsageAsync(tenantId, cancellationToken);
        return OkResponse(new CurrentUsageResponseDto
        {
            TenantId = usage.TenantId,
            Year = usage.Year,
            Month = usage.Month,
            EventsReceived = usage.EventsReceived,
            EventsDelivered = usage.EventsDelivered,
            EventsFailed = usage.EventsFailed,
            MonthlyEventLimit = tenant.MonthlyEventLimit,
            Plan = tenant.Plan,
                });
    }
}
