using Asp.Versioning;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
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
[Route("api/v{version:apiVersion}/admin/tenants")]
public sealed class TenantsController(
    ITenantService tenantService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="request">Tenant creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created tenant.</returns>
    /// <response code="201">Tenant created successfully.</response>
    /// <response code="400">Validation error in request payload.</response>
    /// <response code="409">A tenant with the same slug already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<TenantResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<TenantResponseDto>>> CreateAsync(
        [FromBody] CreateTenantRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await tenantService.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    /// <summary>
    /// Gets all tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of tenants.</returns>
    /// <response code="200">Returned the list of tenants.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TenantResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantResponseDto>>>> GetAllAsync(CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var tenant = await tenantService.GetByIdAsync(currentUserContext.TenantId!, cancellationToken);
        if (tenant is null)
        {
            return OkResponse((IReadOnlyList<TenantResponseDto>)Array.Empty<TenantResponseDto>());
        }

        return OkResponse((IReadOnlyList<TenantResponseDto>)new[] { tenant });
    }

    /// <summary>
    /// Gets a tenant by identifier.
    /// </summary>
    /// <param name="id">Tenant id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant when found.</returns>
    /// <response code="200">Tenant found.</response>
    /// <response code="404">Tenant not found.</response>
    [ActionName(nameof(GetByIdAsync))]
    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<TenantResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TenantResponseDto>>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(id);
        var tenant = await tenantService.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return ErrorResponse<TenantResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        return OkResponse(tenant);
    }

    /// <summary>
    /// Updates tenant mutable fields.
    /// </summary>
    /// <param name="id">Tenant id.</param>
    /// <param name="request">Tenant update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated tenant when found.</returns>
    /// <response code="200">Tenant updated successfully.</response>
    /// <response code="400">Validation error in request payload.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<TenantResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TenantResponseDto>>> UpdateAsync(
        string id,
        [FromBody] UpdateTenantRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(id);
        var updated = await tenantService.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return ErrorResponse<TenantResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        return OkResponse(updated);
    }

    /// <summary>
    /// Disables a tenant.
    /// </summary>
    /// <param name="id">Tenant id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Tenant disabled successfully.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.OwnerOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(id);
        var disabled = await tenantService.DisableAsync(id, cancellationToken);
        if (!disabled)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        return NoContent();
    }
}
