using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Services.SafeMode;
using HookBridge.Api.Authorization;
using HookBridge.Api.DTOs;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Controllers;

/// <summary>
/// Admin API for reviewing and safely applying AI recommendation workflow state.
/// </summary>
/// <remarks>
/// These endpoints only change human approval workflow state. The apply endpoint does not execute production actions,
/// retry webhooks, replay dead-letter records, pause endpoints, or apply generated transformation code.
/// </remarks>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOrOwner)]
[Route("api/admin/ai-actions")]
public sealed class AdminAiActionsController(
    IHumanApprovalWorkflowService workflowService,
    IAiSafeModeGuard safeModeGuard,
    IAiDecisionAuditService auditService,
    IAiDecisionEventProducer decisionEventProducer,
    ILogger<AdminAiActionsController> logger) : ApiControllerBase
{
    /// <summary>Gets pending AI actions for admin review.</summary>
    /// <param name="request">Filters and pagination for pending AI recommendations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>Results are always limited to recommendations in <c>PendingReview</c> status.</remarks>
    /// <response code="200">Pending AI actions were returned.</response>
    /// <response code="400">The query request is invalid.</response>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AdminAiActionResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminAiActionResponseDto>>>> GetPendingAsync(
        [FromQuery] AdminAiActionSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateSearchRequest(request);
        if (validationError is not null) return ErrorResponse<IReadOnlyList<AdminAiActionResponseDto>>(StatusCodes.Status400BadRequest, validationError);

        try
        {
            logger.LogInformation("Admin action requested. Operation={Operation} PageNumber={PageNumber} PageSize={PageSize}", "GetPendingAiActions", request.PageNumber, request.PageSize);
            var approvals = await workflowService.SearchPendingAsync(ToWorkflowSearchRequest(request), cancellationToken);
            return OkResponse(approvals.Select(approval => ToResponse(approval, null)).ToArray());
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<IReadOnlyList<AdminAiActionResponseDto>>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving pending admin AI actions.");
            return ErrorResponse<IReadOnlyList<AdminAiActionResponseDto>>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving pending admin AI actions.");
        }
    }

    /// <summary>Gets one AI action approval record.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The AI action was returned.</response>
    /// <response code="400">The approval id is missing.</response>
    /// <response code="404">The approval record does not exist.</response>
    [HttpGet("{approvalId}")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> GetByApprovalIdAsync(string approvalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, "approvalId is required.");

        try
        {
            logger.LogInformation("Admin action requested. Operation={Operation} ApprovalId={ApprovalId}", "GetAiAction", approvalId);
            var approval = await workflowService.GetByIdAsync(approvalId, cancellationToken);
            return approval is null
                ? ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status404NotFound, "AI action approval record was not found.")
                : OkResponse(ToResponse(approval, null));
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error retrieving admin AI action. ApprovalId={ApprovalId}", approvalId);
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the admin AI action.");
        }
    }

    /// <summary>Approves a pending AI recommendation.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="request">Reviewer and comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The AI recommendation was approved.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="404">The approval record does not exist.</response>
    /// <response code="409">The status transition is not valid.</response>
    [HttpPost("{approvalId}/approve")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> ApproveAsync(string approvalId, [FromBody] AdminAiActionReviewRequestDto? request, CancellationToken cancellationToken)
        => ReviewAsync(approvalId, request, AiRecommendationApprovalStatus.Approved, "approve", "AI recommendation approved", cancellationToken);

    /// <summary>Rejects a pending AI recommendation.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="request">Reviewer and comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The AI recommendation was rejected.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="404">The approval record does not exist.</response>
    /// <response code="409">The status transition is not valid.</response>
    [HttpPost("{approvalId}/reject")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> RejectAsync(string approvalId, [FromBody] AdminAiActionReviewRequestDto? request, CancellationToken cancellationToken)
        => ReviewAsync(approvalId, request, AiRecommendationApprovalStatus.Rejected, "reject", "AI recommendation rejected", cancellationToken);

    /// <summary>Marks an AI recommendation as needing more information.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="request">Reviewer and comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The AI recommendation was marked as needing more information.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="404">The approval record does not exist.</response>
    /// <response code="409">The status transition is not valid.</response>
    [HttpPost("{approvalId}/needs-more-info")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> NeedsMoreInfoAsync(string approvalId, [FromBody] AdminAiActionReviewRequestDto? request, CancellationToken cancellationToken)
        => ReviewAsync(approvalId, request, AiRecommendationApprovalStatus.NeedsMoreInfo, "needs-more-info", "AI recommendation marked needs more info", cancellationToken);

    /// <summary>Expires a pending or approved AI recommendation.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The AI recommendation was expired.</response>
    /// <response code="400">The approval id is missing.</response>
    /// <response code="404">The approval record does not exist.</response>
    /// <response code="409">The status transition is not valid.</response>
    [HttpPost("{approvalId}/expire")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> ExpireAsync(string approvalId, CancellationToken cancellationToken)
        => MutateAsync(approvalId, "expire", id => workflowService.ExpireAsync(id, cancellationToken), null, "AI recommendation expired", cancellationToken);

    /// <summary>Marks an approved AI recommendation as applied after safe mode evaluation.</summary>
    /// <param name="approvalId">Approval workflow identifier.</param>
    /// <param name="request">Operator and apply comment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// This only marks workflow state as <c>Applied</c>. It does not retry webhooks, replay dead-letter records,
    /// pause endpoints, apply generated transformation code, or execute any other production action.
    /// </remarks>
    /// <response code="200">The AI recommendation was marked applied.</response>
    /// <response code="400">The request is invalid.</response>
    /// <response code="404">The approval record does not exist.</response>
    /// <response code="409">The status transition is invalid or AI Safe Mode blocked apply.</response>
    [HttpPost("{approvalId}/apply")]
    [ProducesResponseType(typeof(ApiResponse<AdminAiActionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> ApplyAsync(string approvalId, [FromBody] AdminAiActionApplyRequestDto? request, CancellationToken cancellationToken)
    {
        var validationError = ValidateApplyRequest(approvalId, request);
        if (validationError is not null) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, validationError);

        try
        {
            logger.LogInformation("Admin action requested. Operation={Operation} ApprovalId={ApprovalId}", "apply", approvalId);
            var existing = await workflowService.GetByIdAsync(approvalId, cancellationToken);
            if (existing is null) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status404NotFound, "AI action approval record was not found.");
            if (existing.ApprovalStatus != AiRecommendationApprovalStatus.Approved)
            {
                logger.LogWarning("Invalid transition attempted. ApprovalId={ApprovalId} FromStatus={FromStatus} ToStatus={ToStatus}", approvalId, existing.ApprovalStatus, AiRecommendationApprovalStatus.Applied);
                return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status409Conflict, $"Invalid approval status transition from {existing.ApprovalStatus} to Applied.");
            }

            var safeMode = await safeModeGuard.EvaluateAsync(BuildSafeModeRequest(existing, request!.AppliedBy), cancellationToken);
            if (!safeMode.IsAllowed)
            {
                logger.LogWarning("Safe mode blocked apply. ApprovalId={ApprovalId} SafeModeDecision={SafeModeDecision}", approvalId, safeMode.Decision);
                return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status409Conflict, "AI Safe Mode blocked applying this recommendation.");
            }

            var updated = await workflowService.ApplyAsync(approvalId, new HumanApprovalWorkflowApplyRequestDto
            {
                AppliedBy = request.AppliedBy,
                ApplyComment = request.ApplyComment
            }, cancellationToken);

            if (updated is null) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status404NotFound, "AI action approval record was not found.");
            logger.LogInformation("AI recommendation applied. ApprovalId={ApprovalId} RecommendationId={RecommendationId}", updated.ApprovalId, updated.RecommendationId);
            await AuditAndPublishBestEffortAsync(updated, safeMode, request.AppliedBy, cancellationToken);
            return OkResponse(ToResponse(updated, safeMode));
        }
        catch (AiRecommendationApprovalConflictException ex)
        {
            logger.LogWarning("Invalid transition attempted. ApprovalId={ApprovalId} Operation={Operation}", approvalId, "apply");
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (ValidationException ex)
        {
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error applying admin AI action. ApprovalId={ApprovalId}", approvalId);
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while applying the admin AI action.");
        }
    }

    private async Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> ReviewAsync(
        string approvalId,
        AdminAiActionReviewRequestDto? request,
        AiRecommendationApprovalStatus status,
        string operation,
        string logMessage,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateReviewRequest(approvalId, request);
        if (validationError is not null) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, validationError);

        return await MutateAsync(
            approvalId,
            operation,
            id => workflowService.ReviewAsync(id, new HumanApprovalWorkflowReviewRequestDto
            {
                ApprovalStatus = status,
                ReviewedBy = request!.ReviewedBy,
                ReviewComment = request.ReviewComment
            }, cancellationToken),
            request!.ReviewedBy,
            logMessage,
            cancellationToken);
    }

    private async Task<ActionResult<ApiResponse<AdminAiActionResponseDto>>> MutateAsync(
        string approvalId,
        string operation,
        Func<string, Task<HumanApprovalWorkflowResponseDto?>> action,
        string? actor,
        string logMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, "approvalId is required.");

        try
        {
            logger.LogInformation("Admin action requested. Operation={Operation} ApprovalId={ApprovalId}", operation, approvalId);
            var updated = await action(approvalId);
            if (updated is null) return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status404NotFound, "AI action approval record was not found.");
            logger.LogInformation("{LogMessage}. ApprovalId={ApprovalId} RecommendationId={RecommendationId}", logMessage, updated.ApprovalId, updated.RecommendationId);
            await AuditAndPublishBestEffortAsync(updated, null, actor, cancellationToken);
            return OkResponse(ToResponse(updated, null));
        }
        catch (AiRecommendationApprovalConflictException ex)
        {
            logger.LogWarning("Invalid transition attempted. ApprovalId={ApprovalId} Operation={Operation}", approvalId, operation);
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error processing admin AI action. Operation={Operation} ApprovalId={ApprovalId}", operation, approvalId);
            return ErrorResponse<AdminAiActionResponseDto>(StatusCodes.Status500InternalServerError, "An unexpected error occurred while processing the admin AI action.");
        }
    }

    private async Task AuditAndPublishBestEffortAsync(HumanApprovalWorkflowResponseDto approval, AiSafeModeEvaluationResponseDto? safeMode, string? actor, CancellationToken cancellationToken)
    {
        var decisionId = $"admin_ai_action_{approval.ApprovalId}_{approval.ApprovalStatus}_{Guid.NewGuid():N}";
        var auditRequest = new AiDecisionAuditCreateRequestDto
        {
            DecisionId = decisionId,
            AgentName = nameof(AdminAiActionsController),
            DecisionType = AiDecisionAuditType.HumanApproval,
            Decision = approval.ApprovalStatus.ToString(),
            RiskLevel = approval.RiskLevel,
            SuggestedAction = approval.SuggestedAction,
            RequiresApproval = approval.RequiresApproval,
            ApprovalId = approval.ApprovalId,
            ApprovalStatus = approval.ApprovalStatus.ToString(),
            SafeModeDecision = safeMode?.Decision.ToString(),
            IsActionAllowed = safeMode?.IsAllowed,
            UsedAi = false,
            UsedFallback = false,
            Summary = approval.Summary,
            Recommendation = approval.Recommendation,
            CreatedBy = actor ?? approval.ReviewedBy ?? approval.AppliedBy ?? "HookBridge.Api"
        };

        await auditService.AuditHumanApprovalAsync(auditRequest, cancellationToken);
        await PublishDecisionEventBestEffortAsync(approval, decisionId, safeMode, cancellationToken);
    }

    private async Task PublishDecisionEventBestEffortAsync(HumanApprovalWorkflowResponseDto approval, string decisionId, AiSafeModeEvaluationResponseDto? safeMode, CancellationToken cancellationToken)
    {
        try
        {
            var result = await decisionEventProducer.PublishAsync(new AiDecisionEventDto
            {
                DecisionId = decisionId,
                AgentName = nameof(AdminAiActionsController),
                DecisionType = AiDecisionEventType.HumanApproval,
                Decision = approval.ApprovalStatus.ToString(),
                RiskLevel = approval.RiskLevel,
                SuggestedAction = approval.SuggestedAction,
                RequiresApproval = approval.RequiresApproval,
                ApprovalId = approval.ApprovalId,
                ApprovalStatus = approval.ApprovalStatus.ToString(),
                SafeModeDecision = safeMode?.Decision.ToString(),
                IsActionAllowed = safeMode?.IsAllowed,
                UsedAi = false,
                UsedFallback = false,
                Summary = approval.Summary,
                Recommendation = approval.Recommendation,
                CreatedAtUtc = DateTime.UtcNow,
                Source = "HookBridge.Api"
            }, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("AI decision event publish failed for admin AI action. ApprovalId={ApprovalId} DecisionId={DecisionId} Topic={Topic} Reason={Reason}", approval.ApprovalId, decisionId, result.Topic, result.ErrorMessage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI decision event publish failed for admin AI action. ApprovalId={ApprovalId} DecisionId={DecisionId}", approval.ApprovalId, decisionId);
        }
    }

    private static AiSafeModeEvaluationRequestDto BuildSafeModeRequest(HumanApprovalWorkflowResponseDto approval, string requestedBy) => new()
    {
        ActionType = MapActionType(approval.SuggestedAction),
        Environment = "admin-workflow",
        RiskLevel = approval.RiskLevel,
        ApprovalId = approval.ApprovalId,
        ApprovalStatus = approval.ApprovalStatus,
        RequestedBy = requestedBy,
        Reason = "Admin apply marks approval workflow state only and does not execute production actions.",
        RequestedAtUtc = DateTime.UtcNow
    };

    private static AiActionType MapActionType(string? suggestedAction)
    {
        if (string.IsNullOrWhiteSpace(suggestedAction)) return AiActionType.NotifyOnly;
        if (suggestedAction.Contains("retry", StringComparison.OrdinalIgnoreCase)) return AiActionType.RetryWebhook;
        if (suggestedAction.Contains("replay", StringComparison.OrdinalIgnoreCase)) return AiActionType.ReplayDeadLetter;
        if (suggestedAction.Contains("dead-letter", StringComparison.OrdinalIgnoreCase) || suggestedAction.Contains("deadletter", StringComparison.OrdinalIgnoreCase)) return AiActionType.MoveToDeadLetter;
        if (suggestedAction.Contains("pause", StringComparison.OrdinalIgnoreCase)) return AiActionType.PauseEndpoint;
        if (suggestedAction.Contains("resume", StringComparison.OrdinalIgnoreCase)) return AiActionType.ResumeEndpoint;
        if (suggestedAction.Contains("transform", StringComparison.OrdinalIgnoreCase)) return AiActionType.ApplyTransformation;
        if (suggestedAction.Contains("validation", StringComparison.OrdinalIgnoreCase)) return AiActionType.ApplyValidationRule;
        if (suggestedAction.Contains("config", StringComparison.OrdinalIgnoreCase)) return AiActionType.UpdateConfiguration;
        return AiActionType.NotifyOnly;
    }

    private static AdminAiActionResponseDto ToResponse(HumanApprovalWorkflowResponseDto approval, AiSafeModeEvaluationResponseDto? safeMode) => new()
    {
        ApprovalId = approval.ApprovalId,
        RecommendationId = approval.RecommendationId,
        RecommendationType = approval.RecommendationType,
        ApprovalStatus = approval.ApprovalStatus,
        RiskLevel = approval.RiskLevel,
        SuggestedAction = approval.SuggestedAction,
        RequiresApproval = approval.RequiresApproval,
        CanApply = approval.CanApply,
        SafeModeDecision = safeMode?.Decision,
        IsActionAllowed = safeMode?.IsAllowed,
        Summary = approval.Summary,
        Recommendation = approval.Recommendation,
        RequestedBy = approval.RequestedBy,
        ReviewedBy = approval.ReviewedBy,
        ReviewComment = approval.ReviewComment,
        AppliedBy = approval.AppliedBy,
        ApplyComment = approval.ApplyComment,
        CreatedAtUtc = EnsureUtc(approval.CreatedAtUtc),
        ReviewedAtUtc = EnsureUtc(approval.ReviewedAtUtc),
        AppliedAtUtc = EnsureUtc(approval.AppliedAtUtc),
        ExpiresAtUtc = EnsureUtc(approval.ExpiresAtUtc)
    };

    private static AiRecommendationApprovalSearchRequestDto ToWorkflowSearchRequest(AdminAiActionSearchRequestDto request) => new()
    {
        CustomerId = request.CustomerId,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        RecommendationType = request.RecommendationType,
        ApprovalStatus = AiRecommendationApprovalStatus.PendingReview,
        RiskLevel = request.RiskLevel,
        FromUtc = request.FromUtc,
        ToUtc = request.ToUtc,
        PageNumber = request.PageNumber,
        PageSize = request.PageSize
    };

    private static string? ValidateReviewRequest(string approvalId, AdminAiActionReviewRequestDto? request)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return "approvalId is required.";
        if (request is null) return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.ReviewedBy)) return "ReviewedBy is required.";
        if (request.ReviewComment?.Length > 1_000) return "ReviewComment must be 1000 characters or fewer.";
        return null;
    }

    private static string? ValidateApplyRequest(string approvalId, AdminAiActionApplyRequestDto? request)
    {
        if (string.IsNullOrWhiteSpace(approvalId)) return "approvalId is required.";
        if (request is null) return "Request body is required.";
        if (string.IsNullOrWhiteSpace(request.AppliedBy)) return "AppliedBy is required.";
        if (request.ApplyComment?.Length > 1_000) return "ApplyComment must be 1000 characters or fewer.";
        return null;
    }

    private static string? ValidateSearchRequest(AdminAiActionSearchRequestDto request)
    {
        if (request.PageNumber <= 0) return "PageNumber must be greater than 0.";
        if (request.PageSize is < 1 or > 500) return "PageSize must be between 1 and 500.";
        if (request.FromUtc.HasValue && request.FromUtc.Value.Kind != DateTimeKind.Utc) return "FromUtc must be UTC.";
        if (request.ToUtc.HasValue && request.ToUtc.Value.Kind != DateTimeKind.Utc) return "ToUtc must be UTC.";
        if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.ToUtc.Value <= request.FromUtc.Value) return "ToUtc must be greater than FromUtc.";
        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? EnsureUtc(DateTime? value)
        => value.HasValue ? EnsureUtc(value.Value) : null;
}
