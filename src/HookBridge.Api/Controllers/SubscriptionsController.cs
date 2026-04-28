using Asp.Versioning;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Subscriptions;
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
[Route("api/v{version:apiVersion}/admin/subscriptions")]
public sealed class SubscriptionsController(
    ISubscriptionService subscriptionService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SubscriptionResponseDto>>> CreateAsync(
        [FromBody] CreateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(request.TenantId);
        var created = await subscriptionService.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SubscriptionResponseDto>>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
        {
            return ErrorResponse<SubscriptionResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(subscription.TenantId);
        return OkResponse(subscription);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponseDto<SubscriptionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResponseDto<SubscriptionResponseDto>>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventType,
        [FromQuery] string? targetUrl,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var subscriptions = await subscriptionService.SearchAsync(
            new SubscriptionSearchRequestDto
            {
                TenantId = currentUserContext.TenantId,
                EventType = eventType,
                TargetUrl = targetUrl,
                IsActive = isActive,
                PageNumber = pageNumber,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection,
            },
            cancellationToken);

        return OkResponse(subscriptions);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SubscriptionResponseDto>>> UpdateAsync(
        string id,
        [FromBody] UpdateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        var existing = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return ErrorResponse<SubscriptionResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var updated = await subscriptionService.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return ErrorResponse<SubscriptionResponseDto>(StatusCodes.Status404NotFound, "Not found.");
        }

        return OkResponse(updated);
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
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var deleted = await subscriptionService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
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
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var enabled = await subscriptionService.EnableAsync(id, cancellationToken);
        if (!enabled)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
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
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(existing.TenantId);

        var disabled = await subscriptionService.DisableAsync(id, cancellationToken);
        if (!disabled)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        return NoContent();
    }
}
