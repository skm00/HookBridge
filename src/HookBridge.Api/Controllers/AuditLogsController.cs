using Asp.Versioning;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.AuditLogs;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/audit-logs")]
public sealed class AuditLogsController(
    IAuditLogService auditLogService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(PagedResponseDto<AuditLogResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponseDto<AuditLogResponseDto>>> SearchAsync(
        [FromQuery] string? userId,
        [FromQuery] string? userEmail,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] string? resourceId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var result = await auditLogService.SearchAsync(new AuditLogSearchRequestDto
        {
            TenantId = currentUserContext.TenantId,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(AuditLogResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var log = await auditLogService.GetByIdAsync(id, cancellationToken);
        if (log is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(log.TenantId);
        return Ok(log);
    }
}
