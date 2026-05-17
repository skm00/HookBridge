using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.SafeMode;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-safe-mode")]
public sealed class AiSafeModeController(
    IAiSafeModeGuard safeModeGuard,
    ILogger<AiSafeModeController> logger) : ApiControllerBase
{
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(ApiResponse<AiSafeModeEvaluationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiSafeModeEvaluationResponseDto>>> EvaluateAsync(
        [FromBody] AiSafeModeEvaluationRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ErrorResponse<AiSafeModeEvaluationResponseDto>(StatusCodes.Status400BadRequest, "Safe mode evaluation request is required.");
        }

        var validationResults = request.Validate(new ValidationContext(request)).ToArray();
        if (validationResults.Length > 0)
        {
            var errors = validationResults
                .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty).Select(member => new { Member = member, result.ErrorMessage }))
                .GroupBy(item => item.Member)
                .ToDictionary(group => group.Key, group => group.Select(item => item.ErrorMessage ?? "Invalid value.").ToArray());
            return ErrorResponse<AiSafeModeEvaluationResponseDto>(StatusCodes.Status400BadRequest, "Safe mode evaluation request is invalid.", errors);
        }

        try
        {
            var response = await safeModeGuard.EvaluateAsync(request, cancellationToken);
            logger.LogInformation("Safe mode API evaluation completed. ActionType: {ActionType}, Decision: {Decision}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Decision, response.Environment, request.EventId, request.CorrelationId);
            return OkResponse(response);
        }
        catch (ValidationException ex)
        {
            return ErrorResponse<AiSafeModeEvaluationResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error evaluating AI safe mode action. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", request.ActionType, request.Environment, request.EventId, request.CorrelationId);
            return ErrorResponse<AiSafeModeEvaluationResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while evaluating AI safe mode.");
        }
    }
}
