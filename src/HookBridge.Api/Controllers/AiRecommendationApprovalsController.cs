using HookBridge.AI.Worker.Approval;
using HookBridge.Api.Authorization;
using HookBridge.AI.Worker.DTOs;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-recommendations/approvals")]
public sealed class AiRecommendationApprovalsController(
    IAiRecommendationApprovalService approvalService,
    ILogger<AiRecommendationApprovalsController> logger) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
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
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
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
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<AiRecommendationApprovalResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AiRecommendationApprovalResponseDto>>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!IsValidApprovalId(id))
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, "Approval id must be a valid ObjectId.");
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
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<AiRecommendationApprovalResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
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
            return Created($"/api/ai-recommendations/approvals/{approval.Id}", ApiResponseFactory.Success(approval, "AI recommendation approval created.", TraceId));
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
            logger.LogError(ex, "Unexpected error creating AI recommendation approval. RecommendationId={RecommendationId} RecommendationType={RecommendationType}", request.RecommendationId, request.RecommendationType);
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the AI recommendation approval.");
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
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
        if (!IsValidApprovalId(id) || request is null)
        {
            return ErrorResponse<AiRecommendationApprovalResponseDto>(StatusCodes.Status400BadRequest, "Approval id must be a valid ObjectId and request body is required.");
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

    private static bool IsValidApprovalId(string? id)
        => !string.IsNullOrWhiteSpace(id) && ObjectId.TryParse(id, out _);
}
