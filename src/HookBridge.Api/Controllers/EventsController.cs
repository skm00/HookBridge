using Asp.Versioning;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events/{tenantId}")]
[EnableRateLimiting(RateLimitingPolicyNames.EventIngestionPolicy)]
public sealed class EventsController(IEventIngestionService eventIngestionService) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EventIngestionResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<EventIngestionResponseDto>>> IngestAsync(
        string tenantId,
        [FromBody] EventIngestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader) || string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized.");
        }

        var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var correlationHeader)
            ? correlationHeader.ToString()
            : null;

        try
        {
            var response = await eventIngestionService.IngestAsync(
                tenantId,
                apiKeyHeader.ToString(),
                request,
                correlationId,
                cancellationToken);

            return AcceptedResponse(response);
        }
        catch (UnauthorizedException)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized.");
        }
        catch (TooManyRequestsException)
        {
            return ErrorResponse(StatusCodes.Status429TooManyRequests, "Rate limit exceeded. Please try again later.");
        }
    }
}
