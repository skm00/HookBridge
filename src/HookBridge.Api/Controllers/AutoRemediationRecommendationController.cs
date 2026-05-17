using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-remediation/events")]
public sealed class AutoRemediationRecommendationController(
    IAutoRemediationRecommendationRepository repository,
    ILogger<AutoRemediationRecommendationController> logger) : ApiControllerBase
{
    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(ApiResponse<AutoRemediationRecommendationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AutoRemediationRecommendationResponseDto>>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ErrorResponse<AutoRemediationRecommendationResponseDto>(StatusCodes.Status400BadRequest, "EventId is required.");
        }

        logger.LogInformation("Auto-remediation recommendation lookup requested. EventId: {EventId}", eventId);

        try
        {
            var result = await repository.GetByEventIdAsync(eventId, cancellationToken);
            if (result is null)
            {
                logger.LogInformation("Auto-remediation recommendation not found. EventId: {EventId}", eventId);
                return ErrorResponse<AutoRemediationRecommendationResponseDto>(StatusCodes.Status404NotFound, "Auto-remediation recommendation was not found.");
            }

            return OkResponse(result.ToResponseDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving auto-remediation recommendation. EventId: {EventId}", eventId);
            return ErrorResponse<AutoRemediationRecommendationResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the auto-remediation recommendation.");
        }
    }
}
