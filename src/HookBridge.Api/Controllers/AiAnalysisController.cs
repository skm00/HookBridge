using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Mappers;
using HookBridge.Application.DTOs.AiAnalysis;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-analysis/events")]
public sealed class AiAnalysisController(
    IAiAnalysisResultRepository aiAnalysisResultRepository,
    ILogger<AiAnalysisController> logger) : ApiControllerBase
{
    /// <summary>
    /// Gets the stored AI analysis result for a webhook event.
    /// </summary>
    /// <remarks>
    /// Retrieves the AI-generated failure summary, root cause, retry recommendation, risk level,
    /// confidence score, and model metadata stored for the supplied webhook event identifier.
    /// The endpoint returns only the stored analysis metadata and does not expose webhook payload data.
    /// </remarks>
    /// <param name="eventId">The webhook event identifier used when the event was ingested.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>The stored AI analysis result for the event.</returns>
    /// <response code="200">The AI analysis result was found.</response>
    /// <response code="400">The event identifier is empty or invalid.</response>
    /// <response code="404">No AI analysis result exists for the supplied event identifier.</response>
    /// <response code="500">An unexpected error occurred while retrieving the AI analysis result.</response>
    [HttpGet("{eventId}")]
    [ProducesResponseType(typeof(ApiResponse<AiAnalysisResultResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiAnalysisResultResponseDto>>> GetByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ErrorResponse<AiAnalysisResultResponseDto>(
                StatusCodes.Status400BadRequest,
                "EventId is required.");
        }

        logger.LogInformation("AI analysis lookup requested. EventId={EventId}", eventId);

        try
        {
            var result = await aiAnalysisResultRepository.GetByEventIdAsync(eventId, cancellationToken);
            if (result is null)
            {
                logger.LogInformation("AI analysis result not found. EventId={EventId}", eventId);
                return ErrorResponse<AiAnalysisResultResponseDto>(
                    StatusCodes.Status404NotFound,
                    "AI analysis result was not found.");
            }

            return OkResponse(AiAnalysisResultMapper.ToResponseDto(result));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI analysis result. EventId={EventId}", eventId);
            return ErrorResponse<AiAnalysisResultResponseDto>(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while retrieving the AI analysis result.");
        }
    }
}
