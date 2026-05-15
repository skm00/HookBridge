using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

[ApiController]
[Route("api/ai-recommendations/approvals")]
public sealed class AiRecommendationApprovalsController(
    IAiRecommendationApprovalService approvalService,
    ILogger<AiRecommendationApprovalsController> logger) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>>> SearchAsync(
        [FromQuery] AiRecommendationApprovalSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var approvals = await approvalService.SearchAsync(request, cancellationToken);
            return OkResponse(approvals);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error searching AI recommendation approvals.");
            return ErrorResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while searching AI recommendation approvals.");
        }
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>>> GetPendingAsync(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var approvals = await approvalService.GetPendingAsync(limit, cancellationToken);
            return OkResponse(approvals);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving pending AI recommendation approvals.");
            return ErrorResponse<IReadOnlyList<AiRecommendationApprovalResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving pending AI recommendation approvals.");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AiRecommendationApprovalResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiRecommendationApprovalResponseDto>>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, "Approval id is required.");
        }

        try
        {
            var approval = await approvalService.GetByIdAsync(id, cancellationToken);
            return approval is null
                ? ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status404NotFound, "AI recommendation approval was not found.")
                : OkResponse(approval);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving AI recommendation approval. ApprovalId={ApprovalId}", id);
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the AI recommendation approval.");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AiRecommendationApprovalResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiRecommendationApprovalResponseDto>>> CreateAsync(
        [FromBody] AiRecommendationApprovalCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, "Request body is required.");
        }

        try
        {
            var approval = await approvalService.CreateAsync(request, cancellationToken);
            return CreatedResponse(nameof(GetByIdAsync), new { id = approval.Id }, approval, "AI recommendation approval created.");
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error creating AI recommendation approval. RecommendationId={RecommendationId} RecommendationType={RecommendationType}", request.RecommendationId, request.RecommendationType);
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the AI recommendation approval.");
        }
    }

    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<AiRecommendationApprovalResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiRecommendationApprovalResponseDto>>> UpdateStatusAsync(
        string id,
        [FromBody] AiRecommendationApprovalUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id) || request is null)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, "Approval id and request body are required.");
        }

        try
        {
            var approval = await approvalService.UpdateStatusAsync(id, request, cancellationToken);
            return approval is null
                ? ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status404NotFound, "AI recommendation approval was not found.")
                : OkResponse(approval);
        }
        catch (AiRecommendationApprovalConflictException ex)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error updating AI recommendation approval status. ApprovalId={ApprovalId}", id);
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while updating the AI recommendation approval.");
        }
    }
}
