using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/v1/events/{tenantId}")]
public sealed class EventsController(IEventIngestionService eventIngestionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EventIngestionResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
    }
}
