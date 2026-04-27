using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Api.Authorization;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/subscriptions")]
public sealed class SubscriptionsController(
    ISubscriptionService subscriptionService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(SubscriptionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionResponseDto>> CreateAsync(
        [FromBody] CreateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(request.TenantId);
        var created = await subscriptionService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(SubscriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(subscription.TenantId);
        return Ok(subscription);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponseDto>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventType,
        [FromQuery] string? targetUrl,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var subscriptions = await subscriptionService.SearchAsync(
            new SubscriptionSearchRequestDto
            {
                TenantId = currentUserContext.TenantId,
                EventType = eventType,
                TargetUrl = targetUrl,
                IsActive = isActive,
            },
            cancellationToken);

        return Ok(subscriptions);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(SubscriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponseDto>> UpdateAsync(
        string id,
        [FromBody] UpdateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        var existing = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var updated = await subscriptionService.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var deleted = await subscriptionService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{id}/enable")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var enabled = await subscriptionService.EnableAsync(id, cancellationToken);
        if (!enabled)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{id}/disable")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var disabled = await subscriptionService.DisableAsync(id, cancellationToken);
        if (!disabled)
        {
            return NotFound();
        }

        return NoContent();
    }
}
