using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.RetryAgent;

public sealed class RetryAgent : IRetryAgent
{
    private const long LargePayloadThresholdBytes = 1024 * 1024;
    private readonly RetryAgentOptions _options;
    private readonly ILogger<RetryAgent> _logger;

    public RetryAgent(IOptions<RetryAgentOptions> options, ILogger<RetryAgent> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<RetryAgentResponseDto> AnalyzeAsync(RetryAgentRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        var validationResults = request.Validate(new ValidationContext(request)).ToArray();
        if (validationResults.Length > 0)
        {
            _logger.LogWarning("Invalid retry agent request. EventId: {EventId}, CorrelationId: {CorrelationId}, ValidationErrorCount: {ValidationErrorCount}", request.EventId, request.CorrelationId, validationResults.Length);
            throw new ValidationException(validationResults[0].ErrorMessage);
        }

        _logger.LogInformation("Retry agent started. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, EndpointId: {EndpointId}, StatusCode: {StatusCode}, RetryCount: {RetryCount}, MaxRetryCount: {MaxRetryCount}", request.EventId, request.CorrelationId, request.CustomerId, request.EndpointId, request.StatusCode, request.RetryCount, request.MaxRetryCount);

        var reasonCodes = new HashSet<RetryAgentReasonCode>();
        var riskLevel = NormalizeRiskLevel(request.EndpointRiskLevel);
        var decision = DetermineDecision(request, riskLevel, reasonCodes);
        var requiresApproval = DetermineRequiresApproval(request, riskLevel, reasonCodes, decision);
        var delay = CalculateDelaySeconds(decision, request.RetryCount);
        var confidence = Clamp(CalculateConfidence(decision, reasonCodes));

        if (requiresApproval)
        {
            _logger.LogInformation("Retry agent approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, RetryDecision: {RetryDecision}, RiskLevel: {RiskLevel}", request.EventId, request.CorrelationId, decision, riskLevel);
        }

        var response = new RetryAgentResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            RetryDecision = decision,
            RetryDelaySeconds = delay,
            MaxAllowedRetries = request.MaxRetryCount,
            RiskLevel = riskLevel,
            RequiresApproval = requiresApproval,
            Summary = BuildSummary(request, decision, reasonCodes),
            Recommendation = BuildRecommendation(decision, requiresApproval),
            ReasonCodes = reasonCodes.Count == 0 ? [RetryAgentReasonCode.Unknown] : reasonCodes.ToList(),
            ConfidenceScore = confidence,
            GeneratedAtUtc = DateTime.UtcNow,
            Fallback = !_options.Enabled
        };

        _logger.LogInformation("Retry decision calculated. EventId: {EventId}, CorrelationId: {CorrelationId}, RetryDecision: {RetryDecision}, RetryDelaySeconds: {RetryDelaySeconds}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}, ConfidenceScore: {ConfidenceScore}", response.EventId, response.CorrelationId, response.RetryDecision, response.RetryDelaySeconds, response.RiskLevel, response.RequiresApproval, response.ConfidenceScore);
        return Task.FromResult(response);
    }

    public int CalculateDelaySeconds(RetryAgentDecision decision, int retryCount)
    {
        if (decision == RetryAgentDecision.RetryWithFixedDelay) return ApplyJitter(_options.FixedDelaySeconds);
        if (decision != RetryAgentDecision.RetryWithExponentialBackoff) return 0;

        var exponent = Math.Min(Math.Max(retryCount, 0), 30);
        var computed = _options.BaseDelaySeconds * Math.Pow(2, exponent);
        var capped = (int)Math.Min(Math.Max(0, computed), Math.Max(0, _options.MaxDelaySeconds));
        return ApplyJitter(capped);
    }

    private RetryAgentDecision DetermineDecision(RetryAgentRequestDto request, string riskLevel, ISet<RetryAgentReasonCode> reasonCodes)
    {
        AddTextSignals(request, reasonCodes);
        if (request.PayloadSizeBytes > LargePayloadThresholdBytes) reasonCodes.Add(RetryAgentReasonCode.LargePayload);

        if (IsCritical(riskLevel))
        {
            reasonCodes.Add(RetryAgentReasonCode.EndpointCriticalRisk);
            return RetryAgentDecision.PauseEndpoint;
        }

        if (request.MaxRetryCount >= 0 && request.RetryCount >= request.MaxRetryCount)
        {
            reasonCodes.Add(RetryAgentReasonCode.MaxRetryReached);
            return RetryAgentDecision.MoveToDeadLetter;
        }

        return request.StatusCode switch
        {
            429 => Add(reasonCodes, RetryAgentReasonCode.RateLimited, RetryAgentDecision.RetryWithExponentialBackoff),
            408 or 504 => Add(reasonCodes, RetryAgentReasonCode.Timeout, RetryAgentDecision.RetryWithExponentialBackoff),
            500 or 502 or 503 => Add(reasonCodes, RetryAgentReasonCode.ServerError, RetryAgentDecision.RetryWithExponentialBackoff),
            400 => Add(reasonCodes, RetryAgentReasonCode.ClientError, RetryAgentDecision.RequireManualReview),
            401 => Add(reasonCodes, RetryAgentReasonCode.AuthenticationFailure, RetryAgentDecision.RequireManualReview),
            403 => Add(reasonCodes, RetryAgentReasonCode.AuthorizationFailure, RetryAgentDecision.RequireManualReview),
            404 => Add(reasonCodes, RetryAgentReasonCode.NotFound, RetryAgentDecision.MoveToDeadLetter),
            null => Add(reasonCodes, RetryAgentReasonCode.Unknown, RetryAgentDecision.RequireManualReview),
            _ => Add(reasonCodes, RetryAgentReasonCode.Unknown, RetryAgentDecision.RequireManualReview)
        };
    }

    private bool DetermineRequiresApproval(RetryAgentRequestDto request, string riskLevel, ISet<RetryAgentReasonCode> reasonCodes, RetryAgentDecision decision)
    {
        var requiresApproval = decision == RetryAgentDecision.RequireManualReview;
        if (IsHigh(riskLevel) && _options.RequireApprovalForHighRisk)
        {
            reasonCodes.Add(RetryAgentReasonCode.EndpointHighRisk);
            requiresApproval = true;
        }
        if (IsCritical(riskLevel) && _options.RequireApprovalForCriticalRisk)
        {
            reasonCodes.Add(RetryAgentReasonCode.EndpointCriticalRisk);
            requiresApproval = true;
        }
        if (reasonCodes.Contains(RetryAgentReasonCode.AuthenticationFailure) || reasonCodes.Contains(RetryAgentReasonCode.AuthorizationFailure)) requiresApproval = true;
        if (reasonCodes.Contains(RetryAgentReasonCode.ReplayDetected) || reasonCodes.Contains(RetryAgentReasonCode.DuplicateDetected)) requiresApproval = true;
        if (decision is RetryAgentDecision.PauseEndpoint) requiresApproval = true;
        if (requiresApproval) reasonCodes.Add(RetryAgentReasonCode.ManualReviewRequired);
        return requiresApproval;
    }

    private int ApplyJitter(int seconds)
    {
        if (!_options.EnableJitter || seconds <= 0) return seconds;
        var percentage = Math.Clamp(_options.JitterPercentage, 0, 100) / 100.0;
        return Math.Max(0, (int)Math.Round(seconds * (1 + percentage)));
    }

    private static RetryAgentDecision Add(ISet<RetryAgentReasonCode> reasonCodes, RetryAgentReasonCode reasonCode, RetryAgentDecision decision)
    {
        reasonCodes.Add(reasonCode);
        return decision;
    }

    private static void AddTextSignals(RetryAgentRequestDto request, ISet<RetryAgentReasonCode> reasonCodes)
    {
        var text = $"{request.FailureReason} {request.ErrorMessage}";
        if (text.Contains("replay", StringComparison.OrdinalIgnoreCase)) reasonCodes.Add(RetryAgentReasonCode.ReplayDetected);
        if (text.Contains("duplicate", StringComparison.OrdinalIgnoreCase)) reasonCodes.Add(RetryAgentReasonCode.DuplicateDetected);
    }

    private static string NormalizeRiskLevel(string? riskLevel) => Enum.TryParse<AiRiskLevel>(riskLevel, true, out var parsed) ? parsed.ToString() : AiRiskLevel.Unknown.ToString();
    private static bool IsHigh(string riskLevel) => string.Equals(riskLevel, AiRiskLevel.High.ToString(), StringComparison.OrdinalIgnoreCase);
    private static bool IsCritical(string riskLevel) => string.Equals(riskLevel, AiRiskLevel.Critical.ToString(), StringComparison.OrdinalIgnoreCase);
    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
    private static double CalculateConfidence(RetryAgentDecision decision, ISet<RetryAgentReasonCode> reasonCodes) => decision == RetryAgentDecision.RequireManualReview || reasonCodes.Contains(RetryAgentReasonCode.Unknown) ? 0.65 : 0.85;
    private static string BuildSummary(RetryAgentRequestDto request, RetryAgentDecision decision, ISet<RetryAgentReasonCode> reasonCodes) => request.StatusCode == 429 ? "The receiver returned HTTP 429, indicating rate limiting." : $"Deterministic retry agent selected {decision} for HTTP status {request.StatusCode?.ToString() ?? "unknown"}.";
    private static string BuildRecommendation(RetryAgentDecision decision, bool requiresApproval) => requiresApproval ? "Require manual approval before taking retry action." : decision switch
    {
        RetryAgentDecision.RetryWithExponentialBackoff => "Retry with exponential backoff and reduce delivery concurrency when rate limited.",
        RetryAgentDecision.RetryWithFixedDelay => "Retry with a fixed delay.",
        RetryAgentDecision.MoveToDeadLetter => "Move the failed delivery to the dead-letter queue.",
        RetryAgentDecision.PauseEndpoint => "Pause endpoint delivery until the endpoint is reviewed.",
        _ => "Review the failed delivery before retrying."
    };
}
