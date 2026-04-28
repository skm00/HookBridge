using Asp.Versioning;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicyNames.AdminApiPolicy)]
[Route("api/v{version:apiVersion}/admin/delivery-logs")]
public sealed class DeliveryLogsController(
    IDeliveryAttemptService deliveryAttemptService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator,
    ILogger<DeliveryLogsController> logger) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponseDto<DeliveryAttemptResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResponseDto<DeliveryAttemptResponseDto>>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventId,
        [FromQuery] string? subscriptionId,
        [FromQuery] string? eventType,
        [FromQuery] DeliveryStatus? status,
        [FromQuery] int? httpStatusCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? targetUrl,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        CancellationToken cancellationToken = default)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);
        tenantId = currentUserContext.TenantId;

        logger.LogInformation(
            "Searching delivery logs. TenantId: {TenantId}, EventId: {EventId}, SubscriptionId: {SubscriptionId}, Status: {Status}",
            tenantId,
            eventId,
            subscriptionId,
            status);

        var request = new DeliveryAttemptSearchRequestDto
        {
            TenantId = tenantId,
            EventId = eventId,
            SubscriptionId = subscriptionId,
            EventType = eventType,
            Status = status,
            HttpStatusCode = httpStatusCode,
            FromDate = fromDate,
            ToDate = toDate,
            TargetUrl = targetUrl,
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection,
        };

        var result = await deliveryAttemptService.SearchAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<DeliveryAttemptResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DeliveryAttemptResponseDto>>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await deliveryAttemptService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(result.TenantId);
        return OkResponse(result);
    }
}
