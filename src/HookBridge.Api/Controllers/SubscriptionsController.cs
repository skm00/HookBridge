using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/v1/admin/subscriptions")]
public sealed class SubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionResponseDto>> CreateAsync(
        [FromBody] CreateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await subscriptionService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SubscriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        return Ok(subscription);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponseDto>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventType,
        [FromQuery] string? targetUrl,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionService.SearchAsync(
            new SubscriptionSearchRequestDto
            {
                TenantId = tenantId,
                EventType = eventType,
                TargetUrl = targetUrl,
                IsActive = isActive,
            },
            cancellationToken);

        return Ok(subscriptions);
    }
}
