using Asp.Versioning;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
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
[Route("api/v{version:apiVersion}/admin/tenants")]
public sealed class TenantsController(
    ITenantService tenantService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ControllerBase
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
    [ProducesResponseType(typeof(TenantResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantResponseDto>> CreateAsync(
        [FromBody] CreateTenantRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await tenantService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    /// <summary>
    /// Gets all tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of tenants.</returns>
    /// <response code="200">Returned the list of tenants.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantResponseDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var tenant = await tenantService.GetByIdAsync(currentUserContext.TenantId!, cancellationToken);
        if (tenant is null)
        {
            return Ok(Array.Empty<TenantResponseDto>());
        }

        return Ok(new[] { tenant });
    }

    /// <summary>
    /// Gets a tenant by identifier.
    /// </summary>
    /// <param name="id">Tenant id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant when found.</returns>
    /// <response code="200">Tenant found.</response>
    /// <response code="404">Tenant not found.</response>
    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(TenantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(id);
        var tenant = await tenantService.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(tenant);
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
    [ProducesResponseType(typeof(TenantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantResponseDto>> UpdateAsync(
        string id,
        [FromBody] UpdateTenantRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(id);
        var updated = await tenantService.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
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
            return NotFound();
        }

        return NoContent();
    }
}
