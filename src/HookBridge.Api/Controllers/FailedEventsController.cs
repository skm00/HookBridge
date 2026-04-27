using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/failed-events")]
public sealed class FailedEventsController(
    IFailedEventService failedEventService,
    ILogger<FailedEventsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FailedEventResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FailedEventResponseDto>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventId,
        [FromQuery] string? subscriptionId,
        [FromQuery] string? eventType,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Searching failed events. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, EventType: {EventType}, Status: {Status}",
            tenantId,
            eventId,
            subscriptionId,
            eventType,
            status);

        var request = new FailedEventSearchRequestDto
        {
            TenantId = tenantId,
            EventId = eventId,
            SubscriptionId = subscriptionId,
            EventType = eventType,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
        };

        var result = await failedEventService.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FailedEventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailedEventResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await failedEventService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("{id}/retry")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RetryAsync(string id, CancellationToken cancellationToken)
    {
        var failedEvent = await failedEventService.GetByIdAsync(id, cancellationToken);
        if (failedEvent is null)
        {
            return NotFound();
        }

        if (!string.Equals(failedEvent.Status, "DLQ", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Failed event is not retryable." });
        }

        var retryRequested = await failedEventService.RetryAsync(id, cancellationToken);
        if (!retryRequested)
        {
            return BadRequest(new { message = "Failed event is not retryable." });
        }

        return Accepted();
    }
}
