using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-orchestration/events")]
public sealed class AiAgentOrchestrationController(
    IAiAgentOrchestrationRepository orchestrationRepository,
    ILogger<AiAgentOrchestrationController> logger) : ApiControllerBase
{
    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(ApiResponse<AiAgentOrchestrationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiAgentOrchestrationResponseDto>>> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ErrorResponse<AiAgentOrchestrationResponseDto>(
                StatusCodes.Status400BadRequest,
                "EventId is required.");
        }

        logger.LogInformation("AI orchestration lookup requested. EventId: {EventId}", eventId);

        try
        {
            var result = await orchestrationRepository.GetByEventIdAsync(eventId, cancellationToken);
            if (result is null)
            {
                logger.LogInformation("AI orchestration result not found. EventId: {EventId}", eventId);
                return ErrorResponse<AiAgentOrchestrationResponseDto>(
                    StatusCodes.Status404NotFound,
                    "AI orchestration result was not found.");
            }

            return OkResponse(result.ToResponseDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI orchestration result. EventId: {EventId}", eventId);
            return ErrorResponse<AiAgentOrchestrationResponseDto>(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while retrieving the AI orchestration result.");
        }
    }
}
