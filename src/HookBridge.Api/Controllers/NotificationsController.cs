using Asp.Versioning;
using HookBridge.Api.Authorization;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Notifications;
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
[Route("api/v{version:apiVersion}/admin/notifications")]
public sealed class NotificationsController(
    INotificationService notificationService,
    ICurrentUserContext currentUserContext,
    TenantAccessValidator tenantAccessValidator) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<PagedResponseDto<NotificationResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResponseDto<NotificationResponseDto>>>> SearchAsync(
        [FromQuery] string? type,
        [FromQuery] string? severity,
        [FromQuery] bool? isRead,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var request = new NotificationSearchRequestDto
        {
            TenantId = currentUserContext.TenantId,
            Type = type,
            Severity = severity,
            IsRead = isRead,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var result = await notificationService.SearchAsync(request, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<NotificationResponseDto>>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationService.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(notification.TenantId);
        return OkResponse(notification);
    }

    [HttpPost("{id}/read")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var notification = await notificationService.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        tenantAccessValidator.EnsureTenantAccess(notification.TenantId);

        var marked = await notificationService.MarkAsReadAsync(id, cancellationToken);
        if (!marked)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Not found.");
        }

        return NoContent();
    }

    [HttpGet("unread-count")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        tenantAccessValidator.EnsureTenantAccess(currentUserContext.TenantId ?? string.Empty);

        var count = await notificationService.GetUnreadCountAsync(currentUserContext.TenantId!, cancellationToken);
        return OkResponse((object)new { unreadCount = count });
    }
}
