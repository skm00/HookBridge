using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.Fallback;

public sealed class AiFallbackService : IAiFallbackService
{
    private const string MaskedValue = "[MASKED]";

    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key", "X-API-Key", "ConnectionString"
    ];

    private readonly AiOptions _options;
    private readonly IEndpointHealthScoringService _endpointHealthScoringService;
    private readonly ILogger<AiFallbackService> _logger;

    public AiFallbackService(
        IOptions<AiOptions> options,
        IEndpointHealthScoringService endpointHealthScoringService,
        ILogger<AiFallbackService> logger)
    {
        _options = options.Value;
        _endpointHealthScoringService = endpointHealthScoringService;
        _logger = logger;
    }

    public Task<WebhookFailureAnalysisResponseDto> CreateWebhookFailureAnalysisAsync(
        WebhookFailureAnalysisRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default)
        => CreateRetryRecommendationAsync(request, reason, fallbackMessage, durationMs, cancellationToken);

    public Task<WebhookFailureAnalysisResponseDto> CreateRetryRecommendationAsync(
        WebhookFailureAnalysisRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var generatedAtUtc = DateTime.UtcNow;
        var action = GetFallbackAction(request);
        var riskLevel = GetFallbackRiskLevel(request, action);
        var isRetryRecommended = action == SuggestedRetryAction.RetryWithBackoff;
        var message = NormalizeFallbackMessage(reason, fallbackMessage);

        _logger.LogWarning(
            "AI fallback used for retry recommendation. EventId: {EventId}, CorrelationId: {CorrelationId}, FallbackReason: {FallbackReason}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}, StatusCode: {StatusCode}, SuggestedRetryAction: {SuggestedRetryAction}",
            request.EventId,
            request.CorrelationId,
            reason,
            _options.Provider,
            _options.Model,
            durationMs,
            request.StatusCode,
            action);

        var response = new WebhookFailureAnalysisResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            AiSummary = Truncate($"Fallback analysis was used. {message}", _options.MaxFallbackSummaryLength),
            RootCause = BuildFallbackRootCause(request),
            AiRecommendation = BuildFallbackRecommendation(request, action, message),
            RiskLevel = riskLevel,
            ConfidenceScore = GetFallbackConfidence(request, reason),
            SuggestedRetryAction = action,
            IsRetryRecommended = isRetryRecommended,
            GeneratedAtUtc = generatedAtUtc,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = CreateMetadata(reason, message, generatedAtUtc)
        };

        return Task.FromResult(response);
    }

    public Task<AiLogSummaryResponseDto> CreateLogSummaryAsync(
        AiLogSummaryRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        long durationMs = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var logs = request.Logs ?? Array.Empty<AiLogEntryDto>();
        var errorCount = logs.Count(IsError);
        var warningCount = logs.Count(IsWarning);
        var latestError = logs.Where(IsError).OrderByDescending(log => NormalizeTimestamp(log.TimestampUtc)).FirstOrDefault();
        var generatedAtUtc = DateTime.UtcNow;
        var message = NormalizeFallbackMessage(reason, fallbackMessage);

        _logger.LogWarning(
            "AI fallback used for log summary. EventId: {EventId}, CorrelationId: {CorrelationId}, FallbackReason: {FallbackReason}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}, LogCount: {LogCount}",
            request.EventId,
            request.CorrelationId,
            reason,
            _options.Provider,
            _options.Model,
            durationMs,
            logs.Count);

        var summary = BuildLogSummary(logs.Count, errorCount, warningCount);
        var rootCause = latestError is null
            ? (logs.Count == 0 ? "No logs are available to identify a root cause." : "No error-level log entry was available to identify a root cause.")
            : Truncate(SafeFallbackText(latestError.Message, "Most recent error log entry indicates the likely root cause."), _options.MaxFallbackSummaryLength);

        var response = new AiLogSummaryResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Summary = Truncate(summary, _options.MaxFallbackSummaryLength),
            RootCause = rootCause,
            Impact = errorCount > 0
                ? "Webhook delivery or processing may be delayed or failed until the underlying issue is resolved."
                : "No confirmed delivery failure was identified from the provided logs.",
            Recommendation = Truncate($"{message} Review sanitized logs, delivery attempts, target endpoint health, and retry history before taking manual action.", _options.MaxFallbackSummaryLength),
            RiskLevel = DetermineFallbackRisk(errorCount, warningCount),
            ConfidenceScore = logs.Count == 0 ? 0.1 : 0.35,
            GeneratedAtUtc = generatedAtUtc,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = CreateMetadata(reason, message, generatedAtUtc)
        };

        return Task.FromResult(response);
    }

    public Task<EndpointHealthScoreResponseDto> CreateEndpointHealthSummaryAsync(
        EndpointHealthScoreRequestDto request,
        AiFallbackReason reason,
        string fallbackMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var generatedAtUtc = DateTime.UtcNow;
        var response = _endpointHealthScoringService.CalculateHealthScore(request, generatedAtUtc);
        response.Fallback = CreateMetadata(reason, NormalizeFallbackMessage(reason, fallbackMessage), generatedAtUtc);
        return Task.FromResult(response);
    }

    private AiFallbackMetadataDto CreateMetadata(AiFallbackReason reason, string fallbackMessage, DateTime generatedAtUtc)
        => new()
        {
            UsedFallback = reason != AiFallbackReason.None,
            FallbackReason = reason,
            FallbackMessage = fallbackMessage,
            Provider = _options.Provider,
            Model = _options.Model,
            GeneratedAtUtc = DateTime.SpecifyKind(generatedAtUtc, DateTimeKind.Utc)
        };

    private static SuggestedRetryAction GetFallbackAction(WebhookFailureAnalysisRequestDto request)
    {
        if (HasReachedMaxRetryCount(request))
        {
            return SuggestedRetryAction.MoveToDeadLetter;
        }

        return request.StatusCode switch
        {
            429 => SuggestedRetryAction.RetryWithBackoff,
            408 or 504 => SuggestedRetryAction.RetryWithBackoff,
            500 or 502 or 503 => SuggestedRetryAction.RetryWithBackoff,
            404 => SuggestedRetryAction.MoveToDeadLetter,
            400 or 401 or 403 => SuggestedRetryAction.RequireManualReview,
            _ => SuggestedRetryAction.RequireManualReview
        };
    }

    private static AiRiskLevel GetFallbackRiskLevel(WebhookFailureAnalysisRequestDto request, SuggestedRetryAction action)
    {
        if (HasReachedMaxRetryCount(request))
        {
            return AiRiskLevel.Critical;
        }

        return request.StatusCode switch
        {
            429 => AiRiskLevel.Medium,
            408 or 504 => AiRiskLevel.Medium,
            500 or 502 or 503 => AiRiskLevel.High,
            400 => AiRiskLevel.Medium,
            401 or 403 => AiRiskLevel.High,
            404 => AiRiskLevel.High,
            _ => AiRiskLevel.Unknown
        };
    }

    private static string BuildFallbackRootCause(WebhookFailureAnalysisRequestDto request)
        => request.StatusCode switch
        {
            429 => "The target endpoint returned HTTP 429, which usually indicates rate limiting.",
            408 or 504 => "The target endpoint or gateway timed out while processing the webhook delivery.",
            500 or 502 or 503 => "The target endpoint or upstream service returned a transient server-side failure.",
            400 => "The target endpoint rejected the webhook as a bad request.",
            401 or 403 => "The target endpoint rejected the webhook due to authentication or authorization.",
            404 => "The target endpoint returned not found; the webhook target may be removed or misconfigured.",
            _ => "The failure cause is unknown from available status information."
        };

    private static string BuildFallbackRecommendation(WebhookFailureAnalysisRequestDto request, SuggestedRetryAction action, string message)
    {
        if (HasReachedMaxRetryCount(request))
        {
            return $"{message} Max retry count has been reached; move the event to dead letter instead of retrying.";
        }

        return action switch
        {
            SuggestedRetryAction.RetryWithBackoff => $"{message} Retry with exponential backoff and reduce delivery concurrency if rate limiting or transient failures continue.",
            SuggestedRetryAction.MoveToDeadLetter => $"{message} Move the event to dead letter or require manual review before any additional delivery attempt.",
            SuggestedRetryAction.RequireManualReview => $"{message} Require manual review before retrying to avoid unsafe repeated delivery attempts.",
            _ => $"{message} No automatic retry is recommended."
        };
    }

    private static double GetFallbackConfidence(WebhookFailureAnalysisRequestDto request, AiFallbackReason reason)
    {
        if (HasReachedMaxRetryCount(request))
        {
            return 0.9;
        }

        if (request.StatusCode is null)
        {
            return 0.35;
        }

        return reason == AiFallbackReason.AiDisabled ? 0.7 : 0.65;
    }

    private static string BuildLogSummary(int logCount, int errorCount, int warningCount)
    {
        if (logCount == 0)
        {
            return "No logs are available for this webhook event. Deterministic fallback summary completed without LLM output.";
        }

        if (errorCount > 0)
        {
            return $"Rule-based summary found {errorCount} error log(s) and {warningCount} warning log(s); the latest error is the primary diagnostic signal.";
        }

        if (warningCount > 0)
        {
            return $"Rule-based summary found {warningCount} warning log(s) and no error log entries.";
        }

        return "Rule-based summary found no error or warning log entries. Risk is low from the provided logs.";
    }

    private static AiRiskLevel DetermineFallbackRisk(int errorCount, int warningCount)
    {
        if (errorCount >= 3)
        {
            return AiRiskLevel.High;
        }

        if (errorCount > 0)
        {
            return AiRiskLevel.Medium;
        }

        return warningCount > 0 ? AiRiskLevel.Low : AiRiskLevel.Low;
    }

    private static string NormalizeFallbackMessage(AiFallbackReason reason, string message)
        => string.IsNullOrWhiteSpace(message)
            ? $"LLM provider could not be used ({reason}). Deterministic fallback rules were used."
            : message;

    private static bool HasReachedMaxRetryCount(WebhookFailureAnalysisRequestDto request)
        => request.MaxRetryCount > 0 && request.RetryCount >= request.MaxRetryCount;

    private string Truncate(string value, int maxLength)
    {
        var safeMaxLength = Math.Max(1, maxLength);
        return value.Length <= safeMaxLength
            ? value
            : string.Concat(value.AsSpan(0, safeMaxLength), $"... [truncated from {value.Length} to {safeMaxLength} characters]");
    }

    private static string SafeFallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : MaskSensitiveValues(value);

    private static string MaskSensitiveValues(string value)
    {
        var masked = value;

        foreach (var term in SensitiveTerms)
        {
            masked = SensitiveAssignmentRegex(term).Replace(masked, match =>
            {
                var key = match.Groups["key"].Value;
                var separator = match.Groups["separator"].Value;
                return $"{key}{separator}{MaskedValue}";
            });
        }

        return masked;
    }

    private static Regex SensitiveAssignmentRegex(string term)
        => new(
            $@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]""]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static DateTime NormalizeTimestamp(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool IsError(AiLogEntryDto log)
        => log.Level.Contains("error", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("fatal", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarning(AiLogEntryDto log)
        => log.Level.Contains("warn", StringComparison.OrdinalIgnoreCase);
}
