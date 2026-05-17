using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.SafeMode;

public sealed class AiSafeModeGuard : IAiSafeModeGuard
{
    private const double LowConfidenceThreshold = 0.60;
    private readonly AiSafeModeOptions _options;
    private readonly IAiSafeModeAuditRepository? _auditRepository;
    private readonly IAiDecisionAuditService? _decisionAuditService;
    private readonly ILogger<AiSafeModeGuard> _logger;

    public AiSafeModeGuard(IOptions<AiSafeModeOptions> options, ILogger<AiSafeModeGuard> logger, IAiSafeModeAuditRepository? auditRepository = null, IAiDecisionAuditService? decisionAuditService = null)
    {
        _options = options.Value;
        _logger = logger;
        _auditRepository = auditRepository;
        _decisionAuditService = decisionAuditService;
    }

    public async Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        var validationResults = request.Validate(new ValidationContext(request)).ToArray();
        if (validationResults.Length > 0) throw new ValidationException(validationResults[0].ErrorMessage);

        var environment = string.IsNullOrWhiteSpace(request.Environment) ? _options.Environment : request.Environment.Trim();
        _logger.LogInformation("Safe mode evaluation started. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", request.ActionType, environment, request.EventId, request.CorrelationId);

        var response = EvaluateCore(request, environment);
        await StoreAuditRecordAsync(request, response, cancellationToken);
        if (_decisionAuditService is not null)
        {
            await _decisionAuditService.AuditSafeModeEvaluationAsync(new AiDecisionAuditCreateRequestDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                CustomerId = request.CustomerId,
                SubscriptionId = request.SubscriptionId,
                EndpointId = request.EndpointId,
                Environment = response.Environment,
                AgentName = nameof(AiSafeModeGuard),
                DecisionType = AiDecisionAuditType.SafeModeEvaluation,
                Decision = response.Decision.ToString(),
                RiskLevel = request.RiskLevel,
                ConfidenceScore = request.ConfidenceScore,
                RequiresApproval = response.RequiresApproval,
                ApprovalId = request.ApprovalId,
                ApprovalStatus = request.ApprovalStatus,
                SafeModeDecision = response.Decision.ToString(),
                IsActionAllowed = response.IsAllowed,
                UsedAi = false,
                UsedFallback = false,
                Summary = response.Reason,
                Recommendation = response.BlockMessage,
                CreatedBy = request.RequestedBy
            }, cancellationToken);
        }
        LogDecision(response, request);
        return response;
    }

    private AiSafeModeEvaluationResponseDto EvaluateCore(AiSafeModeEvaluationRequestDto request, string environment)
    {
        if (!_options.Enabled)
        {
            return Build(AiSafeModeDecision.Allowed, true, false, "AI Safe Mode is disabled.", null, request.ActionType, environment);
        }

        if (request.ConfidenceScore is < LowConfidenceThreshold)
        {
            return Build(AiSafeModeDecision.RequiresManualReview, false, false, "Confidence score is below 0.60 and requires manual review.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (IsAlwaysAllowed(request.ActionType) || (request.ActionType == AiActionType.ReadOnlyQuery && _options.AllowReadOnlyActions))
        {
            return Build(AiSafeModeDecision.Allowed, true, false, "Read-only or advisory AI action is allowed.", null, request.ActionType, environment);
        }

        if (request.ActionType == AiActionType.ReadOnlyQuery && !_options.AllowReadOnlyActions)
        {
            return Build(AiSafeModeDecision.Blocked, false, false, "Read-only actions are disabled by configuration.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (IsApproved(request.ApprovalStatus))
        {
            return Build(AiSafeModeDecision.Allowed, true, false, "Protected action has approved human approval.", null, request.ActionType, environment);
        }

        if (IsTerminalBlockedApproval(request.ApprovalStatus))
        {
            return Build(AiSafeModeDecision.Blocked, false, false, $"Approval status {request.ApprovalStatus} does not permit action execution.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (IsHighOrCriticalRisk(request.RiskLevel))
        {
            return Build(AiSafeModeDecision.RequiresApproval, false, true, "High or critical risk AI actions require approved human approval.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        var production = IsProduction(environment);
        if (production && _options.BlockProductionActions && IsProtectedProductionAction(request.ActionType))
        {
            return Build(AiSafeModeDecision.RequiresApproval, false, true, BuildProductionApprovalReason(request.ActionType), AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (production && _options.RequireApprovalForAllProductionActions && request.ActionType != AiActionType.ReadOnlyQuery)
        {
            return Build(AiSafeModeDecision.RequiresApproval, false, true, "Production AI actions require approved human approval.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (RequiresApprovalInAllEnvironments(request.ActionType))
        {
            return Build(AiSafeModeDecision.RequiresApproval, false, true, $"{request.ActionType} requires approved human approval in all environments.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        if (!production && !_options.AllowLowRiskActionsInNonProduction && !_options.AllowAutoApplyInDevelopment)
        {
            return Build(AiSafeModeDecision.Blocked, false, false, "Non-production auto-apply is disabled by AI Safe Mode configuration.", AdvisoryBlockMessage(), request.ActionType, environment);
        }

        return Build(AiSafeModeDecision.Allowed, true, false, "AI Safe Mode allows this non-production action under current configuration.", null, request.ActionType, environment);
    }

    private async Task StoreAuditRecordAsync(AiSafeModeEvaluationRequestDto request, AiSafeModeEvaluationResponseDto response, CancellationToken cancellationToken)
    {
        if (_auditRepository is null || (!_options.AuditBlockedActions && response.IsAllowed)) return;
        if (!_options.AuditBlockedActions && !response.IsAllowed) return;

        var record = new AiSafeModeAuditRecord
        {
            ActionType = response.ActionType,
            Decision = response.Decision,
            Environment = response.Environment,
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            RiskLevel = request.RiskLevel,
            ConfidenceScore = request.ConfidenceScore,
            ApprovalId = request.ApprovalId,
            ApprovalStatus = request.ApprovalStatus,
            RequestedBy = request.RequestedBy,
            Reason = response.Reason,
            BlockMessage = response.BlockMessage,
            EvaluatedAtUtc = response.EvaluatedAtUtc
        };
        try
        {
            await _auditRepository.InsertAsync(record, cancellationToken);
            _logger.LogInformation("Audit record stored. ActionType: {ActionType}, Decision: {Decision}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Decision, response.Environment, request.EventId, request.CorrelationId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Safe mode audit insert failed. ActionType: {ActionType}, Decision: {Decision}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Decision, request.EventId, request.CorrelationId);
        }
    }

    private void LogDecision(AiSafeModeEvaluationResponseDto response, AiSafeModeEvaluationRequestDto request)
    {
        if (response.Decision == AiSafeModeDecision.Allowed)
        {
            _logger.LogInformation("Action allowed. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Environment, request.EventId, request.CorrelationId);
        }
        else if (response.Decision == AiSafeModeDecision.RequiresApproval)
        {
            _logger.LogInformation("Approval required. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Environment, request.EventId, request.CorrelationId);
        }
        else if (response.Decision == AiSafeModeDecision.RequiresManualReview)
        {
            _logger.LogInformation("Manual review required. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Environment, request.EventId, request.CorrelationId);
        }
        else if (_options.LogBlockedActions)
        {
            _logger.LogWarning("Action blocked. ActionType: {ActionType}, Environment: {Environment}, EventId: {EventId}, CorrelationId: {CorrelationId}", response.ActionType, response.Environment, request.EventId, request.CorrelationId);
        }
    }

    private static AiSafeModeEvaluationResponseDto Build(AiSafeModeDecision decision, bool allowed, bool requiresApproval, string reason, string? blockMessage, AiActionType actionType, string environment) => new()
    {
        Decision = decision,
        IsAllowed = allowed,
        RequiresApproval = requiresApproval,
        Reason = reason,
        BlockMessage = blockMessage,
        ActionType = actionType,
        Environment = environment,
        EvaluatedAtUtc = DateTime.UtcNow
    };

    private static bool IsAlwaysAllowed(AiActionType actionType) => actionType is AiActionType.GenerateRecommendation or AiActionType.NotifyOnly;
    private static bool IsProduction(string environment) => string.Equals(environment, "prod", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "production", StringComparison.OrdinalIgnoreCase);
    private static bool IsApproved(AiRecommendationApprovalStatus? status) => status == AiRecommendationApprovalStatus.Approved;
    private static bool IsTerminalBlockedApproval(AiRecommendationApprovalStatus? status) => status is AiRecommendationApprovalStatus.PendingReview or AiRecommendationApprovalStatus.Rejected or AiRecommendationApprovalStatus.NeedsMoreInfo or AiRecommendationApprovalStatus.Expired or AiRecommendationApprovalStatus.Applied;
    private static bool IsHighOrCriticalRisk(string? riskLevel) => string.Equals(riskLevel, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(riskLevel, "Critical", StringComparison.OrdinalIgnoreCase);
    private static bool RequiresApprovalInAllEnvironments(AiActionType actionType) => actionType is AiActionType.PauseEndpoint or AiActionType.ApplyTransformation or AiActionType.ApplyValidationRule or AiActionType.UpdateConfiguration;
    private static bool IsProtectedProductionAction(AiActionType actionType) => actionType is AiActionType.RetryWebhook or AiActionType.MoveToDeadLetter or AiActionType.ReplayDeadLetter or AiActionType.ResumeEndpoint or AiActionType.QuarantineEvent or AiActionType.RejectEvent or AiActionType.ScaleWorker or AiActionType.RestartWorker || RequiresApprovalInAllEnvironments(actionType);
    private static string BuildProductionApprovalReason(AiActionType actionType) => actionType switch
    {
        AiActionType.RetryWebhook => "Production retry actions require approved human approval.",
        AiActionType.MoveToDeadLetter => "Production dead-letter moves require approved human approval.",
        AiActionType.ReplayDeadLetter => "Production dead-letter replays require approved human approval.",
        AiActionType.PauseEndpoint => "Production endpoint pauses require approved human approval.",
        AiActionType.ResumeEndpoint => "Production endpoint resumes require approved human approval.",
        AiActionType.QuarantineEvent => "Production quarantines require approved human approval.",
        AiActionType.RejectEvent => "Production traffic rejection requires approved human approval.",
        AiActionType.ApplyTransformation => "Generated transformation code requires approved human approval.",
        AiActionType.ApplyValidationRule => "Generated validation rules require approved human approval.",
        AiActionType.UpdateConfiguration => "Configuration updates require approved human approval.",
        AiActionType.ScaleWorker => "Production worker scaling requires approved human approval.",
        AiActionType.RestartWorker => "Production worker restarts require approved human approval.",
        _ => $"Production {actionType} actions require approved human approval."
    };

    private static string AdvisoryBlockMessage() => "AI recommendation is advisory only. Approve this action before applying it.";
}
