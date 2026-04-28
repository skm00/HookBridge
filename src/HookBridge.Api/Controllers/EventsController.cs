using Asp.Versioning;
using HookBridge.Api.RateLimiting;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HookBridge.Api.Controllers;

/// <summary>
/// Accepts tenant events for asynchronous webhook fan-out delivery.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events/{tenantId}")]
[EnableRateLimiting(RateLimitingPolicyNames.EventIngestionPolicy)]
public sealed class EventsController(IEventIngestionService eventIngestionService) : ControllerBase
{
    /// <summary>
    /// Ingests a single event using the <c>x-api-key</c> header for tenant authentication.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventIngestionResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EventIngestionResponseDto>> IngestAsync(
        string tenantId,
        [FromBody] EventIngestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader) || string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return Unauthorized(new { message = "x-api-key header is required." });
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

            return Accepted(response);
        }
        catch (UnauthorizedException)
        {
            return Unauthorized(new { message = "Invalid API key." });
        }
        catch (TooManyRequestsException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
    }
}
