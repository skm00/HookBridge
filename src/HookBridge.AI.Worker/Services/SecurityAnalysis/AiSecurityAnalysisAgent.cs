using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.SecurityAnalysis;

public sealed class AiSecurityAnalysisAgent : IAiSecurityAnalysisAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] XssPatterns = ["<script", "javascript:"];
    private static readonly string[] SqlPatterns = ["DROP TABLE", "UNION SELECT"];
    private static readonly string[] CommandPatterns = ["cmd.exe", "/bin/sh", "powershell"];
    private static readonly string[] PathTraversalPatterns = ["../", "..\\"];
    private static readonly string[] Base64Patterns = ["base64,"];
    private static readonly string[] SecretPatterns = ["Bearer ", "password", "client_secret", "access_token"];

    private readonly AiOptions _options;
    private readonly IAiSecurityAnalysisPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<AiSecurityAnalysisAgent> _logger;

    public AiSecurityAnalysisAgent(IOptions<AiOptions> options, IAiSecurityAnalysisPromptBuilder promptBuilder, ILocalLlmClient llmClient, ILogger<AiSecurityAnalysisAgent> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<AiSecurityAnalysisResponseDto> AnalyzeAsync(AiSecurityAnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (!_options.Enabled)
        {
            return CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic security scanning was used.");
        }

        if (!IsPayloadValidJsonWhenText(request.Payload))
        {
            return CreateFallback(request, AiFallbackReason.InvalidJson, "Payload is not valid JSON; deterministic security scanning was used.");
        }

        try
        {
            var prompt = _promptBuilder.BuildPrompt(request);
            var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);
            if (!llmResponse.IsSuccess)
            {
                return CreateFallback(request, llmResponse.FallbackReason, llmResponse.ErrorMessage);
            }

            if (!TryParseResponse(llmResponse.ResponseText, request, out var response, out var failure))
            {
                _logger.LogWarning("AI security analysis response was invalid. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}", request.EventId, request.CorrelationId, failure);
                return CreateFallback(request, AiFallbackReason.InvalidJson, $"AI response could not be used: {failure}");
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI security analysis fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, ex.Message);
        }
    }

    private bool TryParseResponse(string responseText, AiSecurityAnalysisRequestDto request, out AiSecurityAnalysisResponseDto response, out string failure)
    {
        response = new AiSecurityAnalysisResponseDto();
        failure = string.Empty;
        try
        {
            response = JsonSerializer.Deserialize<AiSecurityAnalysisResponseDto>(responseText, JsonOptions) ?? new AiSecurityAnalysisResponseDto();
        }
        catch (JsonException ex)
        {
            failure = $"invalid JSON: {ex.Message}";
            return false;
        }

        response.EventId = string.IsNullOrWhiteSpace(response.EventId) ? request.EventId : response.EventId;
        response.CorrelationId ??= request.CorrelationId;
        response.DetectedSecuritySignals ??= Array.Empty<AiSecuritySignalDto>();
        response.SecurityRiskScore = Math.Clamp(response.SecurityRiskScore, 0, 100);
        response.ConfidenceScore = Math.Clamp(response.ConfidenceScore, 0, 1);
        response.RiskLevel = response.RiskLevel == AiRiskLevel.Unknown ? MapRiskLevel(response.SecurityRiskScore, false) : response.RiskLevel;
        response.SuggestedAction = NormalizeSuggestedAction(response.SuggestedAction, response.RiskLevel, request);
        response.IsSuspicious = response.IsSuspicious || response.SecurityRiskScore > 20 || response.DetectedSecuritySignals.Count > 0;
        response.GeneratedAtUtc = EnsureUtcOrNow(response.GeneratedAtUtc);
        response.Provider = string.IsNullOrWhiteSpace(response.Provider) ? _options.Provider : response.Provider;
        response.Model = string.IsNullOrWhiteSpace(response.Model) ? _options.Model : response.Model;
        response.Fallback = new AiFallbackMetadataDto { UsedFallback = false, FallbackReason = AiFallbackReason.None, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = response.GeneratedAtUtc };
        return true;
    }

    private AiSecurityAnalysisResponseDto CreateFallback(AiSecurityAnalysisRequestDto request, AiFallbackReason reason, string? message)
    {
        if (!_options.EnableSecurityAnalysisFallback)
        {
            return new AiSecurityAnalysisResponseDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                RiskLevel = AiRiskLevel.Unknown,
                Summary = "AI security analysis fallback is disabled.",
                Recommendation = "Enable fallback or retry analysis when the LLM is available.",
                SuggestedAction = AiSecuritySuggestedAction.Monitor,
                ConfidenceScore = 0.1,
                GeneratedAtUtc = DateTime.UtcNow,
                Model = _options.Model,
                Provider = _options.Provider,
                Fallback = CreateFallbackMetadata(reason, message)
            };
        }

        var signals = new List<AiSecuritySignalDto>();
        var score = 0;
        if (request.SignatureValidationFailed) { score += 30; AddSignal(signals, "SignatureValidationFailed", "High", "The webhook signature validation failed.", "signatureValidationFailed=true", "Verify signing secret and timestamp tolerance."); }
        if (request.AuthenticationFailed) { score += 30; AddSignal(signals, "AuthenticationFailed", "High", "Webhook authentication failed.", "authenticationFailed=true", "Verify credentials and authentication header configuration."); }
        if (request.PayloadSizeBytes > _options.LargePayloadThresholdBytes) { score += 15; AddSignal(signals, "LargePayload", "Medium", "Payload exceeds the configured large payload threshold.", $"payloadSizeBytes={request.PayloadSizeBytes}", "Require manual review before replaying unusually large payloads."); }

        var text = BuildSearchText(request);
        if (ContainsAny(text, XssPatterns)) { score += 20; AddSignal(signals, "ScriptContent", "High", "Payload or headers contain script-like content.", "matched script/javascript pattern", "Quarantine and review for XSS before forwarding."); }
        if (ContainsAny(text, SqlPatterns)) { score += 25; AddSignal(signals, "SqlInjectionLikeContent", "High", "Payload or headers contain SQL injection-like strings.", "matched SQL injection-like pattern", "Review payload source and reject if malicious."); }
        if (ContainsAny(text, CommandPatterns)) { score += 30; AddSignal(signals, "CommandInjectionLikeContent", "Critical", "Payload or headers contain command injection-like strings.", "matched command execution pattern", "Quarantine and perform manual security review."); }
        if (ContainsAny(text, PathTraversalPatterns)) { score += 20; AddSignal(signals, "PathTraversalPattern", "High", "Payload or headers contain path traversal patterns.", "matched ../ or ..\\ pattern", "Review payload and reject malicious file path input."); }
        if (ContainsAny(text, Base64Patterns)) { AddSignal(signals, "Base64HeavyContent", "Medium", "Payload or headers contain base64 data URI-like content.", "matched base64, pattern", "Review binary or encoded content before forwarding."); }
        if (ContainsAny(text, SecretPatterns)) { score += 15; AddSignal(signals, "SecretLookingValue", "Medium", "Payload or headers contain token or secret-looking fields.", "matched token/secret keyword", "Mask secrets and verify the sender did not leak credentials."); }
        if (IsSuspiciousUserAgent(request.UserAgent)) { score += 10; AddSignal(signals, "SuspiciousUserAgent", "Low", "User-Agent is missing or unusual.", $"userAgent={request.UserAgent ?? "[not provided]"}", "Monitor sender identity and require review when combined with other signals."); }

        score = Math.Clamp(score, 0, 100);
        var insufficientData = string.IsNullOrWhiteSpace(BuildSearchText(request)) && !request.SignatureValidationFailed && !request.AuthenticationFailed && request.PayloadSizeBytes <= 0;
        var risk = MapRiskLevel(score, insufficientData);
        var action = NormalizeSuggestedAction(MapSuggestedAction(risk, request), risk, request);
        var now = DateTime.UtcNow;
        return new AiSecurityAnalysisResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            IsSuspicious = score > 20 || signals.Count > 0 || request.SignatureValidationFailed || request.AuthenticationFailed,
            SecurityRiskScore = score,
            RiskLevel = risk,
            Summary = signals.Count == 0 ? "No deterministic suspicious security signals were detected." : $"Detected {signals.Count} deterministic webhook security signal(s).",
            Recommendation = BuildFallbackRecommendation(action),
            DetectedSecuritySignals = signals,
            SuggestedAction = action,
            ConfidenceScore = signals.Count == 0 ? 0.45 : 0.6,
            GeneratedAtUtc = now,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = CreateFallbackMetadata(reason, message, now)
        };
    }

    public static AiRiskLevel MapRiskLevel(int score, bool insufficientData = false) => insufficientData ? AiRiskLevel.Unknown : score switch { <= 20 => AiRiskLevel.Low, <= 50 => AiRiskLevel.Medium, <= 80 => AiRiskLevel.High, _ => AiRiskLevel.Critical };

    public static AiSecuritySuggestedAction MapSuggestedAction(AiRiskLevel riskLevel, AiSecurityAnalysisRequestDto request) => riskLevel switch
    {
        AiRiskLevel.Low => request.SignatureValidationFailed || request.AuthenticationFailed ? AiSecuritySuggestedAction.Monitor : AiSecuritySuggestedAction.Allow,
        AiRiskLevel.Medium => AiSecuritySuggestedAction.Monitor,
        AiRiskLevel.High => request.SignatureValidationFailed || request.AuthenticationFailed ? AiSecuritySuggestedAction.Quarantine : AiSecuritySuggestedAction.RequireManualReview,
        AiRiskLevel.Critical => request.SignatureValidationFailed || request.AuthenticationFailed ? AiSecuritySuggestedAction.Reject : AiSecuritySuggestedAction.Quarantine,
        _ => AiSecuritySuggestedAction.Monitor
    };

    private static AiSecuritySuggestedAction NormalizeSuggestedAction(AiSecuritySuggestedAction action, AiRiskLevel risk, AiSecurityAnalysisRequestDto request)
    {
        if ((request.SignatureValidationFailed || request.AuthenticationFailed) && action == AiSecuritySuggestedAction.Allow) return risk is AiRiskLevel.Low or AiRiskLevel.Medium ? AiSecuritySuggestedAction.Monitor : AiSecuritySuggestedAction.Quarantine;
        return action == AiSecuritySuggestedAction.None ? MapSuggestedAction(risk, request) : action;
    }

    private AiFallbackMetadataDto CreateFallbackMetadata(AiFallbackReason reason, string? message, DateTime? now = null) => new() { UsedFallback = true, FallbackReason = reason, FallbackMessage = message ?? string.Empty, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = now ?? DateTime.UtcNow };

    private static void AddSignal(List<AiSecuritySignalDto> signals, string name, string severity, string description, string evidence, string recommendation) => signals.Add(new() { SignalName = name, Severity = severity, Description = description, Evidence = evidence, Recommendation = recommendation });
    private static bool ContainsAny(string value, IEnumerable<string> patterns) => patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    private static bool IsSuspiciousUserAgent(string? userAgent) => string.IsNullOrWhiteSpace(userAgent) || userAgent.Contains("unknown", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("curl", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("sqlmap", StringComparison.OrdinalIgnoreCase);
    private static string BuildFallbackRecommendation(AiSecuritySuggestedAction action) => action switch { AiSecuritySuggestedAction.Allow => "Allow the event and continue normal monitoring.", AiSecuritySuggestedAction.Monitor => "Monitor the event and review if related failures increase.", AiSecuritySuggestedAction.RequireManualReview => "Require manual security review before replaying or forwarding.", AiSecuritySuggestedAction.Quarantine => "Quarantine the event and perform manual security review before replaying or forwarding.", AiSecuritySuggestedAction.Reject => "Reject the event after human confirmation that it is malicious.", _ => "Use manual review before taking irreversible action." };

    private static string BuildSearchText(AiSecurityAnalysisRequestDto request)
    {
        var payload = request.Payload switch { null => string.Empty, string text => text, JsonElement element => element.GetRawText(), _ => JsonSerializer.Serialize(request.Payload, JsonOptions) };
        var headers = request.Headers is null ? string.Empty : string.Join('\n', request.Headers.Select(pair => $"{pair.Key}: {pair.Value}"));
        return string.Join('\n', payload, headers, request.TargetUrl, request.UserAgent, request.EventType);
    }

    private static bool IsPayloadValidJsonWhenText(object? payload)
    {
        if (payload is not string text || string.IsNullOrWhiteSpace(text)) return true;
        try { using var _ = JsonDocument.Parse(text); return true; } catch (JsonException) { return false; }
    }

    private static DateTime EnsureUtcOrNow(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value == default ? DateTime.UtcNow : value, DateTimeKind.Utc);

    private static void ValidateRequest(AiSecurityAnalysisRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) throw new ArgumentException("EventId is required.", nameof(request));
        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be a UTC DateTime.", nameof(request));
        if (request.PayloadSizeBytes < 0) throw new ArgumentException("PayloadSizeBytes must be greater than or equal to zero.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.TargetUrl) && (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))) throw new ArgumentException("TargetUrl must be a valid HTTP or HTTPS URL when provided.", nameof(request));
    }
}
