using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using HookBridge.AI.Worker.Services.SafeMode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.SecurityAgent;

public sealed class SecurityAgent : ISecurityAgent
{
    private static readonly Regex ScriptPattern = new(@"(<\s*script\b|javascript:|onerror\s*=|onload\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlPattern = new(@"(\bunion\s+select\b|\bdrop\s+table\b|\bor\s+1\s*=\s*1\b|;\s*--)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CommandPattern = new(@"(\bcmd\.exe\b|/bin/(ba)?sh\b|\bpowershell\b|\b(?:curl|wget)\s+https?://|;\s*(?:rm|cat|nc)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PathTraversalPattern = new(@"(\.\./|\.\.\\|%2e%2e%2f|%252e%252e%252f)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SecretPattern = new(@"(bearer\s+[A-Za-z0-9._\-]+|client_secret|access_token|refresh_token|api[_-]?key|password\s*[:=]\s*[^\s,}]+|connectionstring)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] SuspiciousUserAgents = ["unknown", "sqlmap", "nikto", "curl", "wget", "python-requests", "bot", "scanner"];

    private readonly SecurityAgentOptions _options;
    private readonly IAiSecurityAnalysisAgent? _aiSecurityAnalysisAgent;
    private readonly IWebhookDuplicateReplayDetectionService? _duplicateReplayService;
    private readonly ILogger<SecurityAgent> _logger;
    private readonly IAiSafeModeGuard? _safeModeGuard;

    public SecurityAgent(
        IOptions<SecurityAgentOptions> options,
        ILogger<SecurityAgent> logger,
        IAiSecurityAnalysisAgent? aiSecurityAnalysisAgent = null,
        IWebhookDuplicateReplayDetectionService? duplicateReplayService = null,
        IAiSafeModeGuard? safeModeGuard = null)
    {
        _options = options.Value;
        _logger = logger;
        _aiSecurityAnalysisAgent = aiSecurityAnalysisAgent;
        _duplicateReplayService = duplicateReplayService;
        _safeModeGuard = safeModeGuard;
    }

    public async Task<SecurityAgentResponseDto> AnalyzeAsync(SecurityAgentRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        _logger.LogInformation("Security agent started. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}", request.EventId, request.CorrelationId, request.CustomerId);

        if (!_options.Enabled)
        {
            return CreateDisabledResponse(request);
        }

        var score = 0;
        var reasonCodes = new HashSet<SecurityAgentReasonCode>();
        var signals = new List<AiSecuritySignalDto>();
        var text = SerializeForScanning(request.Payload);

        AddBooleanSignal(request.SignatureValidationFailed, SecurityAgentReasonCode.SignatureValidationFailed, 30, "High", "Webhook signature validation failed.", "signatureValidationFailed=true", "Verify signing secret and timestamp tolerance.", ref score, reasonCodes, signals);
        AddBooleanSignal(request.AuthenticationFailed, SecurityAgentReasonCode.AuthenticationFailed, 30, "High", "Webhook authentication failed.", "authenticationFailed=true", "Verify webhook credentials and source identity.", ref score, reasonCodes, signals);

        var duplicateReplay = await TryDetectDuplicateReplayAsync(request, cancellationToken);
        var isDuplicate = request.IsDuplicate || duplicateReplay?.IsDuplicate == true;
        var isReplay = request.IsReplay || duplicateReplay?.IsReplay == true;
        AddBooleanSignal(isReplay, SecurityAgentReasonCode.ReplayDetected, 40, "Critical", "Webhook replay was detected.", "isReplay=true", "Quarantine or reject replayed events until reviewed.", ref score, reasonCodes, signals);
        AddBooleanSignal(isDuplicate, SecurityAgentReasonCode.DuplicateDetected, 15, "Medium", "Duplicate webhook was detected.", "isDuplicate=true", "Monitor duplicate volume and review repeated events.", ref score, reasonCodes, signals);

        var payloadPatternReasonCountBefore = reasonCodes.Count;
        AddPatternSignal(ScriptPattern, text, SecurityAgentReasonCode.ScriptContentDetected, 20, "High", "Script-like payload content was detected.", "payload contains script-like pattern", "Quarantine and review payload before forwarding.", ref score, reasonCodes, signals);
        AddPatternSignal(SqlPattern, text, SecurityAgentReasonCode.SqlInjectionPattern, 25, "High", "SQL injection-like payload content was detected.", "payload contains SQL injection-like pattern", "Quarantine and investigate the sender.", ref score, reasonCodes, signals);
        AddPatternSignal(CommandPattern, text, SecurityAgentReasonCode.CommandInjectionPattern, 30, "Critical", "Command injection-like payload content was detected.", "payload contains command injection-like pattern", "Quarantine or reject after manual confirmation.", ref score, reasonCodes, signals);
        AddPatternSignal(PathTraversalPattern, text, SecurityAgentReasonCode.PathTraversalPattern, 20, "High", "Path traversal-like payload content was detected.", "payload contains path traversal-like pattern", "Require manual review before processing.", ref score, reasonCodes, signals);
        AddPatternSignal(SecretPattern, text, SecurityAgentReasonCode.SecretValueDetected, 15, "Medium", "Secret-looking value was detected in the payload.", "payload contains secret-looking key or token", "Review and redact sensitive values.", ref score, reasonCodes, signals);
        if (HasPayloadPattern(reasonCodes) && reasonCodes.Count > payloadPatternReasonCountBefore && (request.SignatureValidationFailed || request.AuthenticationFailed))
        {
            AddSignal(SecurityAgentReasonCode.SuspiciousPayload, 5, "High", "Suspicious payload content was detected together with an authentication or signature failure.", "payload signal with failed trust validation", "Require security review before forwarding.", ref score, reasonCodes, signals);
        }

        if (request.PayloadSizeBytes > _options.LargePayloadThresholdBytes)
        {
            AddSignal(SecurityAgentReasonCode.LargePayload, 15, "Medium", "Payload size exceeds the configured large payload threshold.", "payloadSizeBytes exceeds threshold", "Monitor or review unusually large payloads.", ref score, reasonCodes, signals);
        }

        if (IsSuspiciousUserAgent(request.UserAgent, request.Headers))
        {
            AddSignal(SecurityAgentReasonCode.SuspiciousUserAgent, 10, "Medium", "Suspicious User-Agent was detected.", "userAgent classified as suspicious", "Monitor source and review related events.", ref score, reasonCodes, signals);
        }

        var aiResponse = await TryRunAiSecurityAnalysisAsync(request, cancellationToken);
        if (aiResponse is not null)
        {
            if (aiResponse.RiskLevel == AiRiskLevel.High) reasonCodes.Add(SecurityAgentReasonCode.HighRiskSecurityFinding);
            if (aiResponse.RiskLevel == AiRiskLevel.Critical) reasonCodes.Add(SecurityAgentReasonCode.CriticalSecurityFinding);
            if (aiResponse.SecurityRiskScore > score) score = Math.Min(100, Math.Max(score, aiResponse.SecurityRiskScore));
            signals.AddRange(aiResponse.DetectedSecuritySignals ?? Array.Empty<AiSecuritySignalDto>());
        }

        score = Math.Clamp(score, 0, 100);
        var riskLevel = MapRiskLevel(score);
        if (riskLevel == AiRiskLevel.High) reasonCodes.Add(SecurityAgentReasonCode.HighRiskSecurityFinding);
        if (riskLevel == AiRiskLevel.Critical) reasonCodes.Add(SecurityAgentReasonCode.CriticalSecurityFinding);
        var decision = MapDecision(riskLevel, reasonCodes);
        var requiresApproval = RequiresApproval(riskLevel, reasonCodes, decision);
        var response = new SecurityAgentResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            IsSuspicious = score > 20 || signals.Count > 0 || isReplay || request.SignatureValidationFailed || request.AuthenticationFailed,
            SecurityDecision = decision,
            SecurityRiskScore = score,
            RiskLevel = riskLevel,
            RequiresApproval = requiresApproval,
            Summary = BuildSummary(reasonCodes, decision),
            Recommendation = BuildRecommendation(decision, requiresApproval),
            SecuritySignals = signals,
            ReasonCodes = reasonCodes.Count == 0 ? [SecurityAgentReasonCode.Unknown] : reasonCodes.ToArray(),
            ConfidenceScore = Math.Clamp(CalculateConfidence(reasonCodes, aiResponse), 0, 1),
            GeneratedAtUtc = DateTime.UtcNow,
            Fallback = aiResponse?.Fallback?.UsedFallback ?? false
        };

        if (signals.Any(signal => signal.SignalName is nameof(SecurityAgentReasonCode.ScriptContentDetected) or nameof(SecurityAgentReasonCode.SqlInjectionPattern) or nameof(SecurityAgentReasonCode.CommandInjectionPattern) or nameof(SecurityAgentReasonCode.PathTraversalPattern)))
        {
            _logger.LogWarning("Suspicious payload detected. EventId: {EventId}, CorrelationId: {CorrelationId}, ReasonCodes: {ReasonCodes}", request.EventId, request.CorrelationId, string.Join(',', response.ReasonCodes));
        }

        await ApplySafeModeAsync(request, response, cancellationToken);

        _logger.LogInformation("Security decision calculated. EventId: {EventId}, CorrelationId: {CorrelationId}, SecurityDecision: {SecurityDecision}, RiskLevel: {RiskLevel}, SecurityRiskScore: {SecurityRiskScore}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.SecurityDecision, response.RiskLevel, response.SecurityRiskScore, response.RequiresApproval);
        if (requiresApproval) _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, SecurityDecision: {SecurityDecision}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.SecurityDecision, response.RiskLevel);
        return response;
    }

    private async Task ApplySafeModeAsync(SecurityAgentRequestDto request, SecurityAgentResponseDto response, CancellationToken cancellationToken)
    {
        if (_safeModeGuard is null) return;
        var actionType = response.SecurityDecision switch
        {
            SecurityAgentDecision.Quarantine => AiActionType.QuarantineEvent,
            SecurityAgentDecision.Reject or SecurityAgentDecision.BlockTemporarily => AiActionType.RejectEvent,
            _ => AiActionType.GenerateRecommendation
        };
        var safeModeResponse = await _safeModeGuard.EvaluateAsync(new AiSafeModeEvaluationRequestDto
        {
            ActionType = actionType,
            Environment = request.Environment ?? "production",
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            RiskLevel = response.RiskLevel.ToString(),
            ConfidenceScore = response.ConfidenceScore,
            RequestedBy = nameof(SecurityAgent),
            Reason = response.Recommendation,
            RequestedAtUtc = DateTime.UtcNow
        }, cancellationToken);
        response.SafeModeDecision = safeModeResponse.Decision;
        response.SafeModeReason = safeModeResponse.Reason;
        response.IsActionAllowed = safeModeResponse.IsAllowed;
        response.RequiresApproval = response.RequiresApproval || safeModeResponse.RequiresApproval;
    }

    private async Task<WebhookDuplicateReplayDetectionResponseDto?> TryDetectDuplicateReplayAsync(SecurityAgentRequestDto request, CancellationToken cancellationToken)
    {
        if (_duplicateReplayService is null || request.IsDuplicate || request.IsReplay) return null;
        try
        {
            return await _duplicateReplayService.DetectAsync(new WebhookDuplicateReplayDetectionRequestDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                CustomerId = request.CustomerId,
                CustomerIdType = request.CustomerIdType,
                SubscriptionId = request.SubscriptionId,
                EndpointId = request.EndpointId,
                Environment = request.Environment,
                EventType = request.EventType,
                Source = request.Source,
                TargetUrl = request.TargetUrl,
                Headers = request.Headers,
                Payload = request.Payload,
                ReceivedAtUtc = request.ReceivedAtUtc
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Duplicate/replay detection unavailable for security agent. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return null;
        }
    }

    private async Task<AiSecurityAnalysisResponseDto?> TryRunAiSecurityAnalysisAsync(SecurityAgentRequestDto request, CancellationToken cancellationToken)
    {
        if (_aiSecurityAnalysisAgent is null) return null;
        try
        {
            return await _aiSecurityAnalysisAgent.AnalyzeAsync(new AiSecurityAnalysisRequestDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                CustomerId = request.CustomerId,
                CustomerIdType = request.CustomerIdType,
                SubscriptionId = request.SubscriptionId,
                EndpointId = request.EndpointId,
                Environment = request.Environment,
                Source = request.Source,
                EventType = request.EventType,
                TargetUrl = request.TargetUrl,
                HttpMethod = request.HttpMethod,
                Headers = request.Headers,
                Payload = request.Payload,
                SourceIp = request.SourceIp,
                UserAgent = request.UserAgent,
                SignatureValidationFailed = request.SignatureValidationFailed,
                AuthenticationFailed = request.AuthenticationFailed,
                PayloadSizeBytes = request.PayloadSizeBytes,
                ReceivedAtUtc = request.ReceivedAtUtc
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI security analysis unavailable for security agent. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return null;
        }
    }

    public static AiRiskLevel MapRiskLevel(int score) => score switch
    {
        < 0 => AiRiskLevel.Unknown,
        <= 20 => AiRiskLevel.Low,
        <= 50 => AiRiskLevel.Medium,
        <= 80 => AiRiskLevel.High,
        <= 100 => AiRiskLevel.Critical,
        _ => AiRiskLevel.Critical
    };

    public static SecurityAgentDecision MapDecision(AiRiskLevel riskLevel, ISet<SecurityAgentReasonCode> reasonCodes)
    {
        if (reasonCodes.Contains(SecurityAgentReasonCode.ReplayDetected)) return SecurityAgentDecision.Quarantine;
        if (reasonCodes.Contains(SecurityAgentReasonCode.DuplicateDetected) && riskLevel is AiRiskLevel.Low or AiRiskLevel.Medium) return SecurityAgentDecision.Monitor;
        if (reasonCodes.Contains(SecurityAgentReasonCode.CommandInjectionPattern) && riskLevel == AiRiskLevel.Critical) return SecurityAgentDecision.Reject;
        if (reasonCodes.Contains(SecurityAgentReasonCode.SqlInjectionPattern) || reasonCodes.Contains(SecurityAgentReasonCode.ScriptContentDetected)) return SecurityAgentDecision.Quarantine;
        return riskLevel switch
        {
            AiRiskLevel.Low => reasonCodes.Contains(SecurityAgentReasonCode.SignatureValidationFailed) || reasonCodes.Contains(SecurityAgentReasonCode.AuthenticationFailed) ? SecurityAgentDecision.Monitor : SecurityAgentDecision.Allow,
            AiRiskLevel.Medium => SecurityAgentDecision.Monitor,
            AiRiskLevel.High => reasonCodes.Contains(SecurityAgentReasonCode.SignatureValidationFailed) || reasonCodes.Contains(SecurityAgentReasonCode.AuthenticationFailed) || reasonCodes.Contains(SecurityAgentReasonCode.PathTraversalPattern) ? SecurityAgentDecision.Quarantine : SecurityAgentDecision.RequireManualReview,
            AiRiskLevel.Critical => SecurityAgentDecision.Quarantine,
            _ => SecurityAgentDecision.Monitor
        };
    }

    public bool ShouldPublishAnomaly(SecurityAgentResponseDto response)
        => response.RiskLevel == AiRiskLevel.High && _options.PublishAnomalyForHighRisk
           || response.RiskLevel == AiRiskLevel.Critical && _options.PublishAnomalyForCriticalRisk;

    private bool RequiresApproval(AiRiskLevel riskLevel, ISet<SecurityAgentReasonCode> reasonCodes, SecurityAgentDecision decision)
        => (_options.RequireApprovalForHighRisk && riskLevel == AiRiskLevel.High)
           || (_options.RequireApprovalForCriticalRisk && riskLevel == AiRiskLevel.Critical)
           || (_options.RequireApprovalForReplay && reasonCodes.Contains(SecurityAgentReasonCode.ReplayDetected))
           || reasonCodes.Contains(SecurityAgentReasonCode.SignatureValidationFailed)
           || reasonCodes.Contains(SecurityAgentReasonCode.AuthenticationFailed)
           || decision is SecurityAgentDecision.Quarantine or SecurityAgentDecision.Reject or SecurityAgentDecision.RequireManualReview;

    private static void ValidateRequest(SecurityAgentRequestDto request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            throw new ArgumentException(string.Join("; ", results.Select(result => result.ErrorMessage)));
        }
    }

    private static bool HasPayloadPattern(ISet<SecurityAgentReasonCode> reasonCodes)
        => reasonCodes.Contains(SecurityAgentReasonCode.ScriptContentDetected)
           || reasonCodes.Contains(SecurityAgentReasonCode.SqlInjectionPattern)
           || reasonCodes.Contains(SecurityAgentReasonCode.CommandInjectionPattern)
           || reasonCodes.Contains(SecurityAgentReasonCode.PathTraversalPattern)
           || reasonCodes.Contains(SecurityAgentReasonCode.SecretValueDetected);

    private static void AddBooleanSignal(bool condition, SecurityAgentReasonCode code, int points, string severity, string description, string evidence, string recommendation, ref int score, ISet<SecurityAgentReasonCode> reasonCodes, ICollection<AiSecuritySignalDto> signals)
    {
        if (condition) AddSignal(code, points, severity, description, evidence, recommendation, ref score, reasonCodes, signals);
    }

    private static void AddPatternSignal(Regex regex, string text, SecurityAgentReasonCode code, int points, string severity, string description, string evidence, string recommendation, ref int score, ISet<SecurityAgentReasonCode> reasonCodes, ICollection<AiSecuritySignalDto> signals)
    {
        if (regex.IsMatch(text)) AddSignal(code, points, severity, description, evidence, recommendation, ref score, reasonCodes, signals);
    }

    private static void AddSignal(SecurityAgentReasonCode code, int points, string severity, string description, string evidence, string recommendation, ref int score, ISet<SecurityAgentReasonCode> reasonCodes, ICollection<AiSecuritySignalDto> signals)
    {
        if (!reasonCodes.Add(code)) return;
        score += points;
        signals.Add(new AiSecuritySignalDto { SignalName = code.ToString(), Severity = severity, Description = description, Evidence = evidence, Recommendation = recommendation });
    }

    private static string SerializeForScanning(object? payload)
    {
        if (payload is null) return string.Empty;
        if (payload is string text) return text;
        try { return JsonSerializer.Serialize(payload); } catch { return payload.ToString() ?? string.Empty; }
    }

    private static bool IsSuspiciousUserAgent(string? userAgent, IDictionary<string, string>? headers)
    {
        var value = userAgent;
        if (string.IsNullOrWhiteSpace(value) && headers is not null)
        {
            var match = headers.FirstOrDefault(kvp => string.Equals(kvp.Key, "User-Agent", StringComparison.OrdinalIgnoreCase));
            value = match.Value;
        }
        return !string.IsNullOrWhiteSpace(value) && SuspiciousUserAgents.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalculateConfidence(ISet<SecurityAgentReasonCode> reasonCodes, AiSecurityAnalysisResponseDto? aiResponse)
    {
        var baseConfidence = reasonCodes.Count == 0 || reasonCodes.SetEquals([SecurityAgentReasonCode.Unknown]) ? 0.7 : 0.82;
        if (reasonCodes.Contains(SecurityAgentReasonCode.ReplayDetected) || reasonCodes.Contains(SecurityAgentReasonCode.CommandInjectionPattern)) baseConfidence = 0.9;
        if (aiResponse is not null) baseConfidence = Math.Max(baseConfidence, aiResponse.ConfidenceScore);
        return baseConfidence;
    }

    private static string BuildSummary(ISet<SecurityAgentReasonCode> reasonCodes, SecurityAgentDecision decision)
        => reasonCodes.Count == 0 ? $"Security agent selected {decision} with no suspicious signals." : $"Webhook event security evaluation selected {decision} due to {string.Join(", ", reasonCodes)}.";

    private static string BuildRecommendation(SecurityAgentDecision decision, bool requiresApproval) => decision switch
    {
        SecurityAgentDecision.Allow => "Allow the event and continue normal monitoring.",
        SecurityAgentDecision.Monitor => "Monitor the event and review if related risk signals increase.",
        SecurityAgentDecision.RequireManualReview => "Require manual security review before replaying or forwarding.",
        SecurityAgentDecision.Quarantine => requiresApproval ? "Quarantine the event and perform manual security review before replaying or forwarding." : "Quarantine the event according to configured controls.",
        SecurityAgentDecision.Reject => "Reject only after approval confirms the event is malicious.",
        SecurityAgentDecision.BlockTemporarily => "Temporarily block the source after approval and continued suspicious behavior.",
        _ => "Use manual review before taking irreversible action."
    };

    private static SecurityAgentResponseDto CreateDisabledResponse(SecurityAgentRequestDto request) => new()
    {
        EventId = request.EventId,
        CorrelationId = request.CorrelationId,
        IsSuspicious = false,
        SecurityDecision = SecurityAgentDecision.Monitor,
        SecurityRiskScore = 0,
        RiskLevel = AiRiskLevel.Low,
        RequiresApproval = false,
        Summary = "Security agent is disabled; event was not evaluated.",
        Recommendation = "Monitor the event until the security agent is enabled.",
        ReasonCodes = [SecurityAgentReasonCode.Unknown],
        ConfidenceScore = 0.5,
        GeneratedAtUtc = DateTime.UtcNow,
        Fallback = true
    };
}
