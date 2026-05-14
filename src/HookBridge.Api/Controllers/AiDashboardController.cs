using HookBridge.Api.Services.AiDashboard;
using HookBridge.Application.DTOs.AiDashboard;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-dashboard")]
public sealed class AiDashboardController(
    IAiDashboardSummaryService dashboardSummaryService,
    ILogger<AiDashboardController> logger) : ApiControllerBase
{
    /// <summary>
    /// Gets AI dashboard summary metrics.
    /// </summary>
    /// <remarks>
    /// Returns aggregate AI analysis counts, anomaly counts, security findings, risk distribution,
    /// retry recommendations, endpoint health distribution, and recent AI findings for the selected UTC window.
    /// If fromUtc and toUtc are omitted, the service uses the configured default lookback window.
    /// </remarks>
    /// <param name="request">Dashboard filters. All date values must be UTC.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>AI dashboard summary metrics for the supplied filters.</returns>
    /// <response code="200">The dashboard summary was generated.</response>
    /// <response code="400">The date range or query parameters are invalid.</response>
    /// <response code="500">An unexpected error occurred while generating the dashboard summary.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<AiDashboardSummaryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiDashboardSummaryResponseDto>>> GetSummaryAsync(
        [FromQuery] AiDashboardSummaryRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await dashboardSummaryService.GetSummaryAsync(request, cancellationToken);
            return OkResponse(summary);
        }
        catch (AiDashboardValidationException ex)
        {
            logger.LogWarning(ex, "Invalid AI dashboard summary request. Field={FieldName}", ex.FieldName);
            return ErrorResponse<AiDashboardSummaryResponseDto>(
                StatusCodes.Status400BadRequest,
                ex.Message,
                new Dictionary<string, string[]> { [ex.FieldName] = [ex.Message] });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error generating AI dashboard summary.");
            return ErrorResponse<AiDashboardSummaryResponseDto>(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while generating the AI dashboard summary.");
        }
    }
}
