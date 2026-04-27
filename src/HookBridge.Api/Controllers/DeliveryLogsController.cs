using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Api.Authorization;
using HookBridge.Api.Security;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/delivery-logs")]
public sealed class DeliveryLogsController(
    IDeliveryAttemptService deliveryAttemptService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator,
    ILogger<DeliveryLogsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(IReadOnlyList<DeliveryAttemptResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeliveryAttemptResponseDto>>> SearchAsync(
        [FromQuery] string? tenantId,
        [FromQuery] string? eventId,
        [FromQuery] string? subscriptionId,
        [FromQuery] string? eventType,
        [FromQuery] DeliveryStatus? status,
        [FromQuery] int? httpStatusCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? targetUrl,
        CancellationToken cancellationToken)
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
        };

        var result = await deliveryAttemptService.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)]
    [ProducesResponseType(typeof(DeliveryAttemptResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryAttemptResponseDto>> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await deliveryAttemptService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        tenantAccessValidator.EnsureTenantAccess(result.TenantId);
        return Ok(result);
    }
}
