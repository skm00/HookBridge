using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-deadletter")]
public sealed class DeadLetterAiAnalysisController(
    IDeadLetterAiAnalysisRepository repository,
    IDeadLetterAiAnalysisService analysisService,
    ILogger<DeadLetterAiAnalysisController> logger) : ApiControllerBase
{
    [HttpGet("events/{eventId}")]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterAiAnalysisResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DeadLetterAiAnalysisResponseDto>>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status400BadRequest, "EventId is required.");
        try
        {
            var result = await repository.GetByEventIdAsync(eventId, cancellationToken);
            return result is null
                ? ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status404NotFound, "Dead-letter AI analysis was not found.")
                : OkResponse(result.ToResponseDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving dead-letter AI analysis. EventId: {EventId}", eventId);
            return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving dead-letter AI analysis.");
        }
    }

    [HttpGet("{deadLetterId}")]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterAiAnalysisResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DeadLetterAiAnalysisResponseDto>>> GetByDeadLetterIdAsync(string deadLetterId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deadLetterId)) return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status400BadRequest, "DeadLetterId is required.");
        try
        {
            var result = await repository.GetByDeadLetterIdAsync(deadLetterId, cancellationToken);
            return result is null
                ? ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status404NotFound, "Dead-letter AI analysis was not found.")
                : OkResponse(result.ToResponseDto());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving dead-letter AI analysis. DeadLetterId: {DeadLetterId}", deadLetterId);
            return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving dead-letter AI analysis.");
        }
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(ApiResponse<DeadLetterAiAnalysisResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<DeadLetterAiAnalysisResponseDto>>> AnalyzeAsync([FromBody] DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken)
    {
        if (request is null) return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status400BadRequest, "Request body is required.");
        try
        {
            var response = await analysisService.AnalyzeAsync(request, cancellationToken);
            return OkResponse(response);
        }
        catch (ValidationException ex)
        {
            return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error analyzing dead-letter event. DeadLetterId: {DeadLetterId}, EventId: {EventId}", request.DeadLetterId, request.EventId);
            return ErrorResponse<DeadLetterAiAnalysisResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while analyzing dead-letter event.");
        }
    }
}
