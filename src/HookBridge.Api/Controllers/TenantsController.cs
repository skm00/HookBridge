using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tenants")]
public sealed class TenantsController(ITenantService tenantService) : ControllerBase
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
    [ProducesResponseType(typeof(IReadOnlyList<TenantResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantResponseDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tenants = await tenantService.GetAllAsync(cancellationToken);
        return Ok(tenants);
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
    [ProducesResponseType(typeof(TenantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
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
    [ProducesResponseType(typeof(TenantResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantResponseDto>> UpdateAsync(
        string id,
        [FromBody] UpdateTenantRequestDto request,
        CancellationToken cancellationToken)
    {
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        var disabled = await tenantService.DisableAsync(id, cancellationToken);
        if (!disabled)
        {
            return NotFound();
        }

        return NoContent();
    }
}
