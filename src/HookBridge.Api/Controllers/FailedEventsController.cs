using Asp.Versioning;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.FailedEvents;
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
[Route("api/v{version:apiVersion}/admin/failed-events")]
public sealed class FailedEventsController(
    IFailedEventService failedEventService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator,
    ILogger<FailedEventsController> logger) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponseDto<FailedEventResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResponseDto<FailedEventResponseDto>>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventId,
        [FromQuery] string? subscriptionId,
        [FromQuery] string? eventType,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);
        tenantId = currentUserContext.TenantId;

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
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
        };

        var result = await failedEventService.SearchAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<FailedEventResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FailedEventResponseDto>>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await failedEventService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(result.TenantId);
        return OkResponse(result);
    }

    [HttpPost("{id}/retry")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> RetryAsync(string id, CancellationToken cancellationToken)
    {
        var failedEvent = await failedEventService.GetByIdAsync(id, cancellationToken);
        if (failedEvent is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(failedEvent.TenantId);

        if (!string.Equals(failedEvent.Status, "DLQ", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Failed event is not retryable.");
        }

        var retryRequested = await failedEventService.RetryAsync(id, cancellationToken);
        if (!retryRequested)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Failed event is not retryable.");
        }

        return AcceptedResponse(new { accepted = true });
    }
}
