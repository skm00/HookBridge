using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;
using HookBridge.Api.Authorization;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace HookBridge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-approval-workflow")]
public sealed class HumanApprovalWorkflowController(
    IHumanApprovalWorkflowService workflowService,
    ILogger<HumanApprovalWorkflowController> logger) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<HumanApprovalWorkflowResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> CreateAsync(
        [FromBody] HumanApprovalWorkflowCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null) return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, "Request body is required.");
        try
        {
            var approval = await workflowService.CreateAsync(request, cancellationToken);
            return Created($"/api/ai-approval-workflow/{approval.ApprovalId}", ApiResponseFactory.Success(approval, "Human approval workflow created.", TraceId));
        }
        catch (AiRecommendationApprovalConflictException ex)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error creating human approval workflow. RecommendationId={RecommendationId} RecommendationType={RecommendationType}", request.RecommendationId, request.RecommendationType);
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the human approval workflow.");
        }
    }

    [HttpGet("{approvalId}")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<HumanApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> GetByIdAsync(string approvalId, CancellationToken cancellationToken)
    {
        if (!IsValidApprovalId(approvalId)) return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, "Approval id must be a valid ObjectId.");
        try
        {
            var approval = await workflowService.GetByIdAsync(approvalId, cancellationToken);
            return approval is null
                ? ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status404NotFound, "Human approval workflow record was not found.")
                : OkResponse(approval);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving human approval workflow. ApprovalId={ApprovalId}", approvalId);
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the human approval workflow.");
        }
    }

    [HttpGet("pending")]
    [Authorize(Policy = AuthorizationPolicies.ViewerOrAbove)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HumanApprovalWorkflowResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HumanApprovalWorkflowResponseDto>>>> GetPendingAsync([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return OkResponse(await workflowService.GetPendingAsync(limit, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving pending human approval workflows.");
            return ErrorResponse<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving pending human approval workflows.");
        }
    }

    [HttpPut("{approvalId}/review")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<HumanApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> ReviewAsync(string approvalId, [FromBody] HumanApprovalWorkflowReviewRequestDto request, CancellationToken cancellationToken)
        => MutateAsync(approvalId, request, id => workflowService.ReviewAsync(id, request, cancellationToken), "review");

    [HttpPut("{approvalId}/apply")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<HumanApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> ApplyAsync(string approvalId, [FromBody] HumanApprovalWorkflowApplyRequestDto request, CancellationToken cancellationToken)
        => MutateAsync(approvalId, request, id => workflowService.ApplyAsync(id, request, cancellationToken), "apply");

    [HttpPut("{approvalId}/expire")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
    [ProducesResponseType(typeof(ApiResponse<HumanApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> ExpireAsync(string approvalId, CancellationToken cancellationToken)
        => MutateAsync<object>(approvalId, new object(), id => workflowService.ExpireAsync(id, cancellationToken), "expire");

    private async Task<ActionResult<ApiResponse<HumanApprovalWorkflowResponseDto>>> MutateAsync<TRequest>(
        string approvalId,
        TRequest? request,
        Func<string, Task<HumanApprovalWorkflowResponseDto?>> action,
        string operation)
    {
        if (!IsValidApprovalId(approvalId) || request is null)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, "Approval id must be a valid ObjectId and request body is required.");
        }

        try
        {
            var approval = await action(approvalId);
            return approval is null
                ? ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status404NotFound, "Human approval workflow record was not found.")
                : OkResponse(approval);
        }
        catch (AiRecommendationApprovalConflictException ex)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error during human approval workflow {Operation}. ApprovalId={ApprovalId}", operation, approvalId);
            return ErrorResponse<HumanApprovalWorkflowResponseDto>(StatusCodes.Status500InternalServerError, $"An unexpected error occurred while attempting to {operation} the human approval workflow.");
        }
    }

    private static bool IsValidApprovalId(string? id)
        => !string.IsNullOrWhiteSpace(id) && ObjectId.TryParse(id, out _);
}
