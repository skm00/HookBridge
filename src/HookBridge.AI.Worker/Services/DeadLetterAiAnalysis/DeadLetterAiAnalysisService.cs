using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services.SafeMode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;

public sealed class DeadLetterAiAnalysisService : IDeadLetterAiAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly DeadLetterAiAnalysisOptions _options;
    private readonly AiOptions _aiOptions;
    private readonly IDeadLetterAiAnalysisPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient? _llmClient;
    private readonly IAiSafeModeGuard? _safeModeGuard;
    private readonly ILogger<DeadLetterAiAnalysisService> _logger;

    public DeadLetterAiAnalysisService(
        IOptions<DeadLetterAiAnalysisOptions> options,
        IOptions<AiOptions> aiOptions,
        IDeadLetterAiAnalysisPromptBuilder promptBuilder,
        ILogger<DeadLetterAiAnalysisService> logger,
        ILocalLlmClient? llmClient = null,
        IAiSafeModeGuard? safeModeGuard = null)
    {
        _options = options.Value;
        _aiOptions = aiOptions.Value;
        _promptBuilder = promptBuilder;
        _logger = logger;
        _llmClient = llmClient;
        _safeModeGuard = safeModeGuard;
    }

    public async Task<DeadLetterAiAnalysisResponseDto> AnalyzeAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        _logger.LogInformation("Dead-letter analysis started. DeadLetterId: {DeadLetterId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, StatusCode: {StatusCode}", request.DeadLetterId, request.EventId, request.CorrelationId, request.CustomerId, request.StatusCode);

        DeadLetterAiAnalysisResponseDto response;
        if (_options.Enabled && _options.EnableAiAnalysis && _aiOptions.Enabled && _llmClient is not null)
        {
            response = await TryAnalyzeWithAiAsync(request, cancellationToken);
        }
        else
        {
            response = CreateFallback(request, AiFallbackReason.AiDisabled, "AI analysis is disabled or unavailable; deterministic dead-letter rules were used.");
        }

        ApplySafetyRules(request, response);
        await ApplySafeModeAsync(request, response, cancellationToken);
        _logger.LogInformation("AI analysis completed. DeadLetterId: {DeadLetterId}, EventId: {EventId}, ReplaySafety: {ReplaySafety}, SuggestedAction: {SuggestedAction}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}, RequiresApproval: {RequiresApproval}", response.DeadLetterId, response.EventId, response.ReplaySafety, response.SuggestedAction, response.RiskLevel, response.ConfidenceScore, response.RequiresApproval);
        return response;
    }

    private async Task<DeadLetterAiAnalysisResponseDto> TryAnalyzeWithAiAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = await _promptBuilder.BuildPromptWithMetadataAsync(request, cancellationToken);
            var llm = await _llmClient!.GenerateAsync(prompt.Content, cancellationToken);
            if (!llm.IsSuccess)
            {
                _logger.LogInformation("Fallback analysis used. DeadLetterId: {DeadLetterId}, EventId: {EventId}, Reason: {Reason}", request.DeadLetterId, request.EventId, llm.FallbackReason);
                return CreateFallback(request, llm.FallbackReason, llm.ErrorMessage);
            }

            if (!TryParseAiResponse(llm.ResponseText, request, out var response))
            {
                _logger.LogInformation("Fallback analysis used. DeadLetterId: {DeadLetterId}, EventId: {EventId}, Reason: InvalidJson", request.DeadLetterId, request.EventId);
                return CreateFallback(request, AiFallbackReason.InvalidJson, "AI response was not valid dead-letter analysis JSON.");
            }

            response.Provider = string.IsNullOrWhiteSpace(response.Provider) ? _aiOptions.Provider : response.Provider;
            response.Model = string.IsNullOrWhiteSpace(response.Model) ? _aiOptions.Model : response.Model;
            response.PromptName = prompt.Metadata.PromptName;
            response.PromptVersion = prompt.Metadata.Version;
            response.PromptHash = prompt.Metadata.Hash;
            response.Fallback = new AiFallbackMetadataDto { UsedFallback = false, Provider = _aiOptions.Provider, Model = _aiOptions.Model, GeneratedAtUtc = DateTime.UtcNow };
            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Fallback analysis used. DeadLetterId: {DeadLetterId}, EventId: {EventId}, Reason: LlmUnavailable", request.DeadLetterId, request.EventId);
            return CreateFallback(request, AiFallbackReason.ProviderUnavailable, ex.Message);
        }
    }

    private bool TryParseAiResponse(string json, DeadLetterAiAnalysisRequestDto request, out DeadLetterAiAnalysisResponseDto response)
    {
        response = new DeadLetterAiAnalysisResponseDto();
        try { response = JsonSerializer.Deserialize<DeadLetterAiAnalysisResponseDto>(json, JsonOptions) ?? new DeadLetterAiAnalysisResponseDto(); }
        catch (JsonException) { return false; }
        if (string.IsNullOrWhiteSpace(response.Summary) || string.IsNullOrWhiteSpace(response.Recommendation)) return false;
        response.DeadLetterId = string.IsNullOrWhiteSpace(response.DeadLetterId) ? request.DeadLetterId : response.DeadLetterId;
        response.EventId = string.IsNullOrWhiteSpace(response.EventId) ? request.EventId : response.EventId;
        response.CorrelationId ??= request.CorrelationId;
        response.ConfidenceScore = Math.Clamp(response.ConfidenceScore, 0, 1);
        response.ConfidenceLevel = response.ConfidenceLevel == AiConfidenceLevel.Unknown ? ToConfidenceLevel(response.ConfidenceScore) : response.ConfidenceLevel;
        response.GeneratedAtUtc = response.GeneratedAtUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc);
        response.ReasonCodes = response.ReasonCodes?.ToArray() ?? Array.Empty<DeadLetterReasonCode>();
        return true;
    }

    private DeadLetterAiAnalysisResponseDto CreateFallback(DeadLetterAiAnalysisRequestDto request, AiFallbackReason reason, string? message)
    {
        var reasonCodes = new List<DeadLetterReasonCode>();
        var (safety, action, risk, rootCause, summary, recommendation) = Classify(request, reasonCodes);
        var confidence = CalculateFallbackConfidence(request, reasonCodes);
        var response = new DeadLetterAiAnalysisResponseDto
        {
            DeadLetterId = request.DeadLetterId.Trim(),
            EventId = request.EventId.Trim(),
            CorrelationId = TrimToNull(request.CorrelationId),
            RootCause = rootCause,
            Summary = summary,
            Recommendation = recommendation,
            ReplaySafety = safety,
            SuggestedAction = action,
            RiskLevel = risk,
            ConfidenceScore = confidence,
            ConfidenceLevel = ToConfidenceLevel(confidence),
            RequiresApproval = RequiresApproval(action, risk, request),
            SafeModeDecision = AiSafeModeDecision.RequiresApproval,
            IsActionAllowed = false,
            ReasonCodes = reasonCodes.Distinct().ToArray(),
            GeneratedAtUtc = DateTime.UtcNow,
            Model = _aiOptions.Model,
            Provider = _aiOptions.Provider,
            Fallback = new AiFallbackMetadataDto { UsedFallback = true, FallbackReason = reason, FallbackMessage = message ?? string.Empty, Provider = _aiOptions.Provider, Model = _aiOptions.Model, GeneratedAtUtc = DateTime.UtcNow },
            PromptName = DeadLetterAiAnalysisPromptBuilder.PromptName,
            PromptVersion = DeadLetterAiAnalysisPromptBuilder.PromptVersion
        };
        _logger.LogInformation("Replay safety calculated. DeadLetterId: {DeadLetterId}, EventId: {EventId}, ReplaySafety: {ReplaySafety}, SuggestedAction: {SuggestedAction}", response.DeadLetterId, response.EventId, response.ReplaySafety, response.SuggestedAction);
        return response;
    }

    private static (DeadLetterReplaySafety Safety, DeadLetterSuggestedAction Action, string Risk, string RootCause, string Summary, string Recommendation) Classify(DeadLetterAiAnalysisRequestDto request, List<DeadLetterReasonCode> reasonCodes)
    {
        if (request.MaxRetryCount >= 0 && request.RetryCount >= request.MaxRetryCount) reasonCodes.Add(DeadLetterReasonCode.MaxRetryReached);
        if (request.IsSuspicious) { reasonCodes.Add(DeadLetterReasonCode.SuspiciousPayload); return (DeadLetterReplaySafety.DoNotReplay, DeadLetterSuggestedAction.Quarantine, "Critical", "Event was flagged as suspicious before or during dead-letter handling.", "Suspicious dead-letter event should not be replayed.", "Quarantine and require human/security review before any action."); }
        if (request.IsReplay) { reasonCodes.Add(DeadLetterReasonCode.ReplayDetected); return (DeadLetterReplaySafety.DoNotReplay, DeadLetterSuggestedAction.Quarantine, "High", "Event appears to be a replay attempt.", "Replay event reached dead-letter and should not be replayed again.", "Quarantine or reject after manual review."); }
        if (request.IsDuplicate) { reasonCodes.Add(DeadLetterReasonCode.DuplicateDetected); return (DeadLetterReplaySafety.DoNotReplay, DeadLetterSuggestedAction.KeepInDeadLetter, "Medium", "Event appears to duplicate a previous webhook delivery.", "Duplicate event should remain in dead-letter.", "Keep in dead-letter unless an operator confirms replay is necessary."); }

        return request.StatusCode switch
        {
            429 => Add(DeadLetterReasonCode.RateLimited, DeadLetterReplaySafety.ReplayWithCaution, DeadLetterSuggestedAction.ReplayWithBackoff, "Medium", "Target endpoint returned HTTP 429 rate limiting responses.", "Event reached dead-letter due to rate limiting.", "Replay only after reducing delivery concurrency and using exponential backoff."),
            408 or 504 => Add(DeadLetterReasonCode.Timeout, DeadLetterReplaySafety.ReplayWithCaution, DeadLetterSuggestedAction.ReplayWithBackoff, "Medium", "Target endpoint timed out during delivery attempts.", "Event reached dead-letter due to timeout failures.", "Replay with backoff after confirming endpoint health."),
            500 or 502 or 503 => Add(DeadLetterReasonCode.ServerError, DeadLetterReplaySafety.ReplayWithCaution, DeadLetterSuggestedAction.ReplayWithBackoff, "Medium", "Target endpoint returned transient server errors.", "Event reached dead-letter due to server-side failures.", "Replay with exponential backoff after endpoint recovery."),
            400 => Add(DeadLetterReasonCode.PayloadContractIssue, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterSuggestedAction.FixPayloadBeforeReplay, "High", "Target endpoint rejected the request as malformed or invalid.", "Payload contract issue likely caused dead-letter delivery.", "Fix payload mapping/contract before considering replay."),
            401 => Add(DeadLetterReasonCode.AuthenticationFailure, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterSuggestedAction.FixAuthenticationBeforeReplay, "High", "Target endpoint rejected the request due to authentication failure.", "Authentication failure caused dead-letter delivery.", "Fix credentials/signature configuration before replay."),
            403 => Add(DeadLetterReasonCode.AuthorizationFailure, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterSuggestedAction.FixAuthenticationBeforeReplay, "High", "Target endpoint rejected the request due to authorization failure.", "Authorization failure caused dead-letter delivery.", "Fix endpoint permissions before replay."),
            404 => Add(DeadLetterReasonCode.NotFound, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterSuggestedAction.FixEndpointBeforeReplay, "High", "Target endpoint returned not found.", "Endpoint URL or route likely no longer exists.", "Fix endpoint URL/route before replay."),
            >= 400 and < 500 => Add(DeadLetterReasonCode.ClientError, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterSuggestedAction.RequireManualReview, "High", "Target endpoint returned a client error.", "Client-side delivery issue requires review.", "Review payload, endpoint, and authentication before replay."),
            null => Add(DeadLetterReasonCode.Unknown, DeadLetterReplaySafety.RequiresManualReview, DeadLetterSuggestedAction.RequireManualReview, "Unknown", "No HTTP status code was supplied.", "Dead-letter cause could not be determined from status code.", "Require manual review before any replay."),
            _ => Add(DeadLetterReasonCode.Unknown, DeadLetterReplaySafety.RequiresManualReview, DeadLetterSuggestedAction.RequireManualReview, "Unknown", "Status code did not match deterministic replay rules.", "Dead-letter cause requires manual review.", "Require manual review before any replay.")
        };

        (DeadLetterReplaySafety, DeadLetterSuggestedAction, string, string, string, string) Add(DeadLetterReasonCode code, DeadLetterReplaySafety safety, DeadLetterSuggestedAction action, string risk, string rootCause, string summary, string recommendation)
        { reasonCodes.Add(code); return (safety, action, risk, rootCause, summary, recommendation); }
    }

    private void ApplySafetyRules(DeadLetterAiAnalysisRequestDto request, DeadLetterAiAnalysisResponseDto response)
    {
        var codes = response.ReasonCodes.ToList();
        if (request.RetryCount >= request.MaxRetryCount && !codes.Contains(DeadLetterReasonCode.MaxRetryReached)) codes.Add(DeadLetterReasonCode.MaxRetryReached);
        if (request.IsSuspicious && response.ReplaySafety == DeadLetterReplaySafety.SafeToReplay) response.ReplaySafety = DeadLetterReplaySafety.DoNotReplay;
        if (request.IsReplay && response.ReplaySafety == DeadLetterReplaySafety.SafeToReplay) response.ReplaySafety = DeadLetterReplaySafety.DoNotReplay;
        if (request.StatusCode is 401 or 403 && response.SuggestedAction == DeadLetterSuggestedAction.Replay) response.SuggestedAction = DeadLetterSuggestedAction.FixAuthenticationBeforeReplay;
        if (request.StatusCode == 400 && response.SuggestedAction == DeadLetterSuggestedAction.Replay) response.SuggestedAction = DeadLetterSuggestedAction.FixPayloadBeforeReplay;
        response.ConfidenceScore = Math.Clamp(response.ConfidenceScore, 0, 1);
        response.ConfidenceLevel = ToConfidenceLevel(response.ConfidenceScore);
        response.GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc == default ? DateTime.UtcNow : response.GeneratedAtUtc, DateTimeKind.Utc);
        response.RequiresApproval = response.RequiresApproval || RequiresApproval(response.SuggestedAction, response.RiskLevel, request);
        if (response.RequiresApproval && !codes.Contains(DeadLetterReasonCode.ManualReviewRequired)) codes.Add(DeadLetterReasonCode.ManualReviewRequired);
        response.ReasonCodes = codes.Distinct().ToArray();
        if (response.RequiresApproval) _logger.LogInformation("Approval required. DeadLetterId: {DeadLetterId}, EventId: {EventId}, SuggestedAction: {SuggestedAction}, RiskLevel: {RiskLevel}", response.DeadLetterId, response.EventId, response.SuggestedAction, response.RiskLevel);
    }

    private async Task ApplySafeModeAsync(DeadLetterAiAnalysisRequestDto request, DeadLetterAiAnalysisResponseDto response, CancellationToken cancellationToken)
    {
        if (_safeModeGuard is null) { response.IsActionAllowed = false; return; }
        var safeMode = await _safeModeGuard.EvaluateAsync(new AiSafeModeEvaluationRequestDto
        {
            ActionType = IsReplayAction(response.SuggestedAction) ? AiActionType.ReplayDeadLetter : AiActionType.ReadOnlyQuery,
            Environment = request.Environment ?? "production",
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            CustomerId = request.CustomerId,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            RiskLevel = response.RiskLevel,
            ConfidenceScore = response.ConfidenceScore,
            RequestedBy = nameof(DeadLetterAiAnalysisService),
            Reason = response.Recommendation,
            RequestedAtUtc = DateTime.UtcNow
        }, cancellationToken);
        response.SafeModeDecision = safeMode.Decision;
        response.IsActionAllowed = safeMode.IsAllowed && !response.RequiresApproval;
        response.RequiresApproval = response.RequiresApproval || safeMode.RequiresApproval;
        _logger.LogInformation("Safe mode evaluated. DeadLetterId: {DeadLetterId}, EventId: {EventId}, SafeModeDecision: {SafeModeDecision}, IsActionAllowed: {IsActionAllowed}", response.DeadLetterId, response.EventId, response.SafeModeDecision, response.IsActionAllowed);
    }

    private bool RequiresApproval(DeadLetterSuggestedAction action, string risk, DeadLetterAiAnalysisRequestDto request)
        => (_options.RequireApprovalForReplay && IsReplayAction(action))
           || (_options.RequireApprovalForHighRisk && string.Equals(risk, "High", StringComparison.OrdinalIgnoreCase))
           || (_options.RequireApprovalForCriticalRisk && string.Equals(risk, "Critical", StringComparison.OrdinalIgnoreCase))
           || (_options.RequireApprovalForSuspiciousEvents && request.IsSuspicious);

    private static bool IsReplayAction(DeadLetterSuggestedAction action) => action is DeadLetterSuggestedAction.Replay or DeadLetterSuggestedAction.ReplayWithBackoff;
    private static double CalculateFallbackConfidence(DeadLetterAiAnalysisRequestDto request, List<DeadLetterReasonCode> codes) => Math.Clamp((request.StatusCode.HasValue ? 0.78 : 0.55) - (codes.Contains(DeadLetterReasonCode.Unknown) ? 0.15 : 0), 0, 1);
    private static AiConfidenceLevel ToConfidenceLevel(double score) => score >= 0.85 ? AiConfidenceLevel.VeryHigh : score >= 0.7 ? AiConfidenceLevel.High : score >= 0.45 ? AiConfidenceLevel.Medium : score > 0 ? AiConfidenceLevel.Low : AiConfidenceLevel.Unknown;
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(DeadLetterAiAnalysisRequestDto request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, true)) throw new ValidationException(string.Join("; ", results.Select(r => r.ErrorMessage)));
    }
}
