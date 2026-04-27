using HookBridge.Application.DTOs.Usage;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v1/admin/tenants/{tenantId}/usage")]
public sealed class UsageController(
    IUsageService usageService,
    IMongoRepository<Tenant> tenantRepository,
    TenantAccessValidator tenantAccessValidator) : ControllerBase
{
    [HttpGet("current")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(CurrentUsageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUsageResponseDto>> GetCurrentAsync(string tenantId, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(tenantId);
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        var usage = await usageService.GetCurrentMonthUsageAsync(tenantId, cancellationToken);
        return Ok(new CurrentUsageResponseDto
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
