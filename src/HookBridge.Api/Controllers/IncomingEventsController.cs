using HookBridge.Api.Authorization;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/events")]
public sealed class IncomingEventsController(
    IIncomingEventQueryService incomingEventQueryService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator,
    ILogger<IncomingEventsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<IncomingEventResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncomingEventResponseDto>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventId,
        [FromQuery] string? eventType,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? correlationId,
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);
        tenantId = currentUserContext.TenantId;

        logger.LogInformation(
            "Searching incoming events. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, Status: {Status}, CorrelationId: {CorrelationId}",
            tenantId,
            eventId,
            eventType,
            status,
            correlationId);

        var request = new IncomingEventSearchRequestDto
        {
            TenantId = tenantId,
            EventId = eventId,
            EventType = eventType,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate,
            CorrelationId = correlationId,
        };

        var result = await incomingEventQueryService.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(IncomingEventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncomingEventResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await incomingEventQueryService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        logger.LogInformation(
            "Getting incoming event by id. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, Status: {Status}, CorrelationId: {CorrelationId}",
            currentUserContext.TenantId,
            result.EventId,
            result.EventType,
            result.Status,
            result.CorrelationId);

        tenantAccessValidator.EnsureTenantAccess(result.TenantId);
        return Ok(result);
    }
}
