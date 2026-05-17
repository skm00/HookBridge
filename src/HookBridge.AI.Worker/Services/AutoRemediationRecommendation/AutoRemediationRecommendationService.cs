using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.AutoRemediationRecommendation;

public sealed class AutoRemediationRecommendationService : IAutoRemediationRecommendationService
{
    private readonly AutoRemediationRecommendationOptions _options;
    private readonly ILogger<AutoRemediationRecommendationService> _logger;

    public AutoRemediationRecommendationService(IOptions<AutoRemediationRecommendationOptions> options, ILogger<AutoRemediationRecommendationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<AutoRemediationRecommendationResponseDto> RecommendAsync(AutoRemediationRecommendationRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.ConfidenceScore = Math.Clamp(request.ConfidenceScore, 0, 1);
        Validate(request);
        _logger.LogInformation("Auto-remediation recommendation started. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, StatusCode: {StatusCode}", request.EventId, request.CorrelationId, request.CustomerId, request.StatusCode);

        var confidence = Math.Clamp(request.ConfidenceScore, 0, 1);
        var risk = string.IsNullOrWhiteSpace(request.RiskLevel) ? "Unknown" : request.RiskLevel.Trim();
        var decision = SelectDecision(request, confidence);
        var requiresApproval = RequiresApproval(decision.Type, decision.Action, risk, confidence, decision.ReasonCodes);
        var canAutoApply = _options.Enabled && _options.AllowAutoApplyLowRisk && !requiresApproval && IsLowRisk(risk);
        if (!_options.AllowAutoApplyLowRisk)
        {
            canAutoApply = false;
        }

        if (requiresApproval && !decision.ReasonCodes.Contains(AutoRemediationReasonCode.HumanApprovalRequired))
        {
            decision.ReasonCodes.Add(AutoRemediationReasonCode.HumanApprovalRequired);
        }

        var response = new AutoRemediationRecommendationResponseDto
        {
            EventId = request.EventId.Trim(),
            CorrelationId = TrimToNull(request.CorrelationId),
            RemediationType = decision.Type,
            RecommendedAction = decision.Action,
            RiskLevel = risk,
            ConfidenceScore = confidence,
            RequiresApproval = requiresApproval,
            CanAutoApply = canAutoApply,
            Summary = decision.Summary,
            Recommendation = decision.Recommendation,
            Steps = decision.Steps,
            ReasonCodes = decision.ReasonCodes,
            GeneratedAtUtc = DateTime.UtcNow
        };

        _logger.LogInformation("Recommendation calculated. EventId: {EventId}, CorrelationId: {CorrelationId}, RemediationType: {RemediationType}, RecommendedAction: {RecommendedAction}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}, RequiresApproval: {RequiresApproval}, CanAutoApply: {CanAutoApply}", response.EventId, response.CorrelationId, response.RemediationType, response.RecommendedAction, response.RiskLevel, response.ConfidenceScore, response.RequiresApproval, response.CanAutoApply);
        if (response.RequiresApproval)
        {
            _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, RemediationType: {RemediationType}, RecommendedAction: {RecommendedAction}", response.EventId, response.CorrelationId, response.RemediationType, response.RecommendedAction);
        }

        return Task.FromResult(response);
    }

    private Decision SelectDecision(AutoRemediationRecommendationRequestDto request, double confidence)
    {
        if (!_options.Enabled) return Manual("Auto-remediation recommendations are disabled.", AutoRemediationReasonCode.Unknown);
        if (confidence < _options.LowConfidenceThreshold) return Manual("Recommendation confidence is below the safe review threshold.", AutoRemediationReasonCode.LowConfidence);
        if (request.IsSuspicious) return Security("Webhook payload or metadata was flagged as suspicious.", AutoRemediationReasonCode.SuspiciousPayload);
        if (request.IsReplay) return Security("Webhook event appears to be a replay attempt.", AutoRemediationReasonCode.ReplayDetected);
        if (request.IsDuplicate) return Manual("Webhook event appears to be a duplicate and should be reviewed before action.", AutoRemediationReasonCode.DuplicateDetected);
        if (IsCritical(request.EndpointHealthStatus)) return EndpointPause("Endpoint health is critical and pausing deliveries should be reviewed.", AutoRemediationReasonCode.EndpointCriticalRisk);
        if (IsCritical(request.ObservabilityStatus)) return Support("Observability status is critical and needs operational escalation.", AutoRemediationReasonCode.EndpointCriticalRisk);
        if (request.MongoIsHealthy == false) return Mongo("MongoDB health check is reporting unhealthy.", AutoRemediationReasonCode.MongoUnhealthy);
        if (request.MongoLatencyMs > _options.MongoLatencyThresholdMs) return Mongo("MongoDB latency is above the configured threshold.", AutoRemediationReasonCode.MongoHighLatency);
        if (request.KafkaConsumerLag > _options.KafkaLagThreshold) return Kafka("Kafka consumer lag is above the configured threshold.", AutoRemediationReasonCode.HighKafkaLag);
        if (request.MaxRetryCount > 0 && request.RetryCount >= request.MaxRetryCount) return DeadLetter("Maximum retry count has been reached.", AutoRemediationReasonCode.MaxRetryReached, AutoRemediationRecommendedAction.MoveToDeadLetter);
        if (request.DeadLetterCount > 0) return DeadLetter("Dead-letter records are present for this scope.", AutoRemediationReasonCode.DeadLetterRecordsFound, AutoRemediationRecommendedAction.ReviewDeadLetterQueue);

        return request.StatusCode switch
        {
            429 => new Decision(AutoRemediationType.RetryTuning, AutoRemediationRecommendedAction.RetryWithBackoff, "Webhook delivery is rate limited by the target endpoint.", "Retry with exponential backoff and reduce delivery concurrency for this endpoint.", ["Keep the event in retry queue.", "Apply exponential backoff for the next retry.", "Review endpoint concurrency limits if 429 responses continue."], [AutoRemediationReasonCode.RateLimited]),
            408 or 504 => new Decision(AutoRemediationType.TimeoutAdjustment, AutoRemediationRecommendedAction.RetryWithBackoff, "Webhook delivery timed out.", "Review endpoint timeout settings and retry with backoff before increasing timeout.", ["Keep the event eligible for retry.", "Apply exponential backoff for the next retry.", "Increase timeout only after human review of endpoint latency."], [AutoRemediationReasonCode.Timeout]),
            500 or 502 or 503 => new Decision(AutoRemediationType.RetryTuning, AutoRemediationRecommendedAction.RetryWithBackoff, "Target endpoint returned a transient server error.", "Retry with exponential backoff and monitor repeated server errors.", ["Keep the event in retry queue.", "Apply exponential backoff.", "Escalate if server errors continue."], [AutoRemediationReasonCode.ServerError]),
            400 => Payload("Target endpoint rejected the payload contract.", AutoRemediationReasonCode.ClientError),
            401 => Credentials("Target endpoint rejected authentication credentials.", AutoRemediationReasonCode.AuthenticationFailure),
            403 => Credentials("Target endpoint rejected authorization for the delivery.", AutoRemediationReasonCode.AuthorizationFailure),
            404 => Payload("Target endpoint route was not found; review URL and payload contract before dead-letter handling.", AutoRemediationReasonCode.ClientError),
            _ => Manual("No deterministic remediation rule matched the supplied event metadata.", AutoRemediationReasonCode.Unknown)
        };
    }

    private bool RequiresApproval(AutoRemediationType type, AutoRemediationRecommendedAction action, string risk, double confidence, IReadOnlyCollection<AutoRemediationReasonCode> reasonCodes)
    {
        if (confidence < _options.LowConfidenceThreshold) return true;
        if (IsHigh(risk) && _options.RequireApprovalForHighRisk) return true;
        if (IsCritical(risk) && _options.RequireApprovalForCriticalRisk) return true;
        if (_options.RequireApprovalForSecurityActions && (type == AutoRemediationType.SecurityQuarantineRecommendation || action == AutoRemediationRecommendedAction.QuarantineEvent)) return true;
        if (_options.RequireApprovalForEndpointPause && (type is AutoRemediationType.EndpointPauseRecommendation or AutoRemediationType.EndpointResumeRecommendation || action is AutoRemediationRecommendedAction.PauseEndpoint or AutoRemediationRecommendedAction.ResumeEndpoint)) return true;
        if (_options.RequireApprovalForDeadLetterActions && (type == AutoRemediationType.DeadLetterReview || action is AutoRemediationRecommendedAction.MoveToDeadLetter or AutoRemediationRecommendedAction.ReviewDeadLetterQueue)) return true;
        return reasonCodes.Contains(AutoRemediationReasonCode.LowConfidence);
    }

    private static Decision Security(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.SecurityQuarantineRecommendation, AutoRemediationRecommendedAction.QuarantineEvent, summary, "Quarantine is recommended only after human approval; do not execute automated production remediation.", ["Preserve the event for forensic review.", "Open a human approval workflow.", "Review security evidence before quarantine."], [reason]);
    private static Decision EndpointPause(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.EndpointPauseRecommendation, AutoRemediationRecommendedAction.PauseEndpoint, summary, "Pause endpoint deliveries only after human approval.", ["Notify operations owner.", "Open a human approval workflow.", "Pause only after approval and customer communication."], [reason]);
    private static Decision Support(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.ManualReview, AutoRemediationRecommendedAction.EscalateToSupport, summary, "Escalate to support for investigation; do not auto-apply remediation.", ["Create support escalation.", "Attach structured event metadata.", "Monitor related health signals."], [reason]);
    private static Decision Mongo(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.MongoHealthInvestigation, AutoRemediationRecommendedAction.CheckMongoHealth, summary, "Investigate MongoDB health before changing delivery behavior.", ["Check MongoDB health dashboards.", "Review latency and error metrics.", "Escalate if database health remains degraded."], [reason]);
    private static Decision Kafka(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.KafkaLagInvestigation, AutoRemediationRecommendedAction.CheckKafkaConsumers, summary, "Investigate Kafka consumers and partitions before operational changes.", ["Check consumer group lag.", "Review partition assignment and worker health.", "Scale consumers only through approved operations workflow."], [reason]);
    private static Decision DeadLetter(string summary, AutoRemediationReasonCode reason, AutoRemediationRecommendedAction action) => new(AutoRemediationType.DeadLetterReview, action, summary, "Review dead-letter records before replaying or moving events.", ["Inspect dead-letter metadata.", "Confirm root cause is resolved.", "Replay or archive only after approval."], [reason]);
    private static Decision Payload(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.PayloadContractReview, AutoRemediationRecommendedAction.ReviewPayloadContract, summary, "Review payload schema and endpoint contract.", ["Compare payload fields with endpoint contract.", "Validate transformations.", "Coordinate schema fixes before retrying."], [reason]);
    private static Decision Credentials(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.CredentialReview, AutoRemediationRecommendedAction.ReviewCredentials, summary, "Review endpoint credentials and authorization configuration.", ["Verify credential status without logging secrets.", "Rotate credentials if required.", "Retry only after authentication is fixed."], [reason]);
    private static Decision Manual(string summary, AutoRemediationReasonCode reason) => new(AutoRemediationType.ManualReview, AutoRemediationRecommendedAction.RequireManualReview, summary, "Manual review is required because no safe auto-remediation is permitted.", ["Review structured metadata.", "Decide the operational action manually.", "Record approval before production changes."], [reason]);

    private static void Validate(AutoRemediationRecommendationRequestDto request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true))
        {
            throw new ValidationException(string.Join(" ", results.Select(result => result.ErrorMessage)));
        }
    }

    private static bool IsHigh(string? value) => string.Equals(value, "High", StringComparison.OrdinalIgnoreCase);
    private static bool IsCritical(string? value) => string.Equals(value, "Critical", StringComparison.OrdinalIgnoreCase);
    private static bool IsLowRisk(string? value) => string.Equals(value, "Low", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase);
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Decision(AutoRemediationType Type, AutoRemediationRecommendedAction Action, string Summary, string Recommendation, IReadOnlyList<string> Steps, List<AutoRemediationReasonCode> ReasonCodes);
}
