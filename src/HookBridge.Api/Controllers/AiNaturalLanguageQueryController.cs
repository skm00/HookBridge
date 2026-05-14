using HookBridge.Api.Services.AiNaturalLanguageQuery;
using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai/query")]
public sealed class AiNaturalLanguageQueryController(
    IAiNaturalLanguageQueryService queryService,
    ILogger<AiNaturalLanguageQueryController> logger) : ApiControllerBase
{
    /// <summary>
    /// Answers a safe natural language question about webhook deliveries, failures, anomalies, retries, endpoint risk, and security findings.
    /// </summary>
    /// <remarks>
    /// The endpoint uses predefined repository methods and filtered, redacted metadata only. It does not execute AI-generated database queries,
    /// expose webhook payloads or secrets, or perform production actions directly. AI output is advisory and falls back to deterministic summaries
    /// when the configured model is disabled or unavailable.
    /// </remarks>
    /// <param name="request">Natural language query plus optional safe filters such as customer, endpoint, event, correlation, and UTC date range.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>A concise answer, detected intent, filters used, matching redacted results, and suggested advisory actions.</returns>
    /// <response code="200">The natural language query was answered.</response>
    /// <response code="400">The request was invalid.</response>
    /// <response code="500">An unexpected error occurred while answering the query.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AiNaturalLanguageQueryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiNaturalLanguageQueryResponseDto>>> QueryAsync(
        [FromBody] AiNaturalLanguageQueryRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await queryService.QueryAsync(request, cancellationToken);
            return OkResponse(response);
        }
        catch (AiNaturalLanguageQueryValidationException ex)
        {
            logger.LogInformation("Invalid AI natural language query request. Field={FieldName} Message={Message}", ex.FieldName, ex.Message);
            return ErrorResponse<AiNaturalLanguageQueryResponseDto>(
                StatusCodes.Status400BadRequest,
                "The AI natural language query request is invalid.",
                new Dictionary<string, string[]> { [ex.FieldName] = [ex.Message] });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error answering AI natural language query.");
            return ErrorResponse<AiNaturalLanguageQueryResponseDto>(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while answering the AI natural language query.");
        }
    }
}
