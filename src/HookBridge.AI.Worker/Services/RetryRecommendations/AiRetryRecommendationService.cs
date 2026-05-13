using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.RetryRecommendations;

public sealed class AiRetryRecommendationService : IAiRetryRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiOptions _options;
    private readonly IWebhookFailurePromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<AiRetryRecommendationService> _logger;

    public AiRetryRecommendationService(
        IOptions<AiOptions> options,
        IWebhookFailurePromptBuilder promptBuilder,
        ILocalLlmClient llmClient,
        ILogger<AiRetryRecommendationService> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<WebhookFailureAnalysisResponseDto> AnalyzeAsync(
        WebhookFailureAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Starting AI retry recommendation analysis. EventId: {EventId}, CorrelationId: {CorrelationId}, StatusCode: {StatusCode}, RetryCount: {RetryCount}, MaxRetryCount: {MaxRetryCount}",
            request.EventId,
            request.CorrelationId,
            request.StatusCode,
            request.RetryCount,
            request.MaxRetryCount);

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "AI retry recommendation fallback used because AI is disabled. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return CreateFallbackResponse(request, "AI is disabled; rule-based retry recommendation was used.");
        }

        try
        {
            var prompt = _promptBuilder.BuildPrompt(request);
            var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);

            if (!TryParseAndValidate(llmResponse, out var parsed, out var validationFailure))
            {
                _logger.LogWarning(
                    "AI retry recommendation response validation failed. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                    request.EventId,
                    request.CorrelationId,
                    validationFailure);
                return CreateFallbackResponse(request, $"AI response could not be used: {validationFailure}");
            }

            var normalized = NormalizeAndApplySafetyRules(parsed, request);
            _logger.LogInformation(
                "AI retry recommendation succeeded. EventId: {EventId}, CorrelationId: {CorrelationId}, SuggestedRetryAction: {SuggestedRetryAction}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}",
                normalized.EventId,
                normalized.CorrelationId,
                normalized.SuggestedRetryAction,
                normalized.RiskLevel,
                normalized.ConfidenceScore);
            return normalized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "AI retry recommendation fallback used because LLM analysis failed. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return CreateFallbackResponse(request, "LLM analysis was unavailable; rule-based retry recommendation was used.");
        }
    }

    private bool TryParseAndValidate(
        string llmResponse,
        out WebhookFailureAnalysisResponseDto response,
        out string failureReason)
    {
        response = new WebhookFailureAnalysisResponseDto();
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(llmResponse))
        {
            failureReason = "empty response";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(llmResponse);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                failureReason = "response root is not a JSON object";
                return false;
            }

            var requiredFields = new[]
            {
                "eventId",
                "aiSummary",
                "rootCause",
                "aiRecommendation",
                "riskLevel",
                "confidenceScore",
                "suggestedRetryAction",
                "isRetryRecommended",
                "generatedAtUtc"
            };

            foreach (var field in requiredFields)
            {
                if (!root.TryGetProperty(field, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    failureReason = $"missing required field '{field}'";
                    return false;
                }
            }

            if (!HasNonEmptyString(root, "aiSummary") ||
                !HasNonEmptyString(root, "rootCause") ||
                !HasNonEmptyString(root, "aiRecommendation"))
            {
                failureReason = "one or more required text fields are empty";
                return false;
            }

            if (!TryGetEnum<AiRiskLevel>(root, "riskLevel", out _))
            {
                failureReason = "riskLevel is not a valid value";
                return false;
            }

            if (!TryGetEnum<SuggestedRetryAction>(root, "suggestedRetryAction", out _))
            {
                failureReason = "suggestedRetryAction is not a valid value";
                return false;
            }

            if (!root.GetProperty("confidenceScore").TryGetDouble(out var confidenceScore) ||
                double.IsNaN(confidenceScore) ||
                double.IsInfinity(confidenceScore))
            {
                failureReason = "confidenceScore is not a finite number";
                return false;
            }

            if (root.GetProperty("isRetryRecommended").ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                failureReason = "isRetryRecommended is not a boolean";
                return false;
            }

            response = JsonSerializer.Deserialize<WebhookFailureAnalysisResponseDto>(llmResponse, JsonOptions)
                ?? new WebhookFailureAnalysisResponseDto();

            return true;
        }
        catch (JsonException exception)
        {
            failureReason = $"invalid JSON ({exception.GetType().Name})";
            return false;
        }
    }

    private WebhookFailureAnalysisResponseDto NormalizeAndApplySafetyRules(
        WebhookFailureAnalysisResponseDto response,
        WebhookFailureAnalysisRequestDto request)
    {
        response.EventId = request.EventId;
        response.CorrelationId = request.CorrelationId;
        response.GeneratedAtUtc = EnsureUtc(response.GeneratedAtUtc);
        response.Model = string.IsNullOrWhiteSpace(response.Model) ? _options.Model : response.Model;
        response.Provider = string.IsNullOrWhiteSpace(response.Provider) ? _options.Provider : response.Provider;
        response.ConfidenceScore = ClampConfidence(response.ConfidenceScore);

        ApplyRetrySafetyRules(response, request);
        return response;
    }

    private WebhookFailureAnalysisResponseDto CreateFallbackResponse(WebhookFailureAnalysisRequestDto request, string reason)
    {
        var action = GetFallbackAction(request);
        var riskLevel = GetFallbackRiskLevel(request, action);
        var isRetryRecommended = action == SuggestedRetryAction.RetryWithBackoff;

        _logger.LogInformation(
            "Rule-based retry recommendation selected. EventId: {EventId}, CorrelationId: {CorrelationId}, StatusCode: {StatusCode}, SuggestedRetryAction: {SuggestedRetryAction}",
            request.EventId,
            request.CorrelationId,
            request.StatusCode,
            action);

        return new WebhookFailureAnalysisResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            AiSummary = BuildFallbackSummary(request),
            RootCause = BuildFallbackRootCause(request),
            AiRecommendation = reason,
            RiskLevel = riskLevel,
            ConfidenceScore = 0.65,
            SuggestedRetryAction = action,
            IsRetryRecommended = isRetryRecommended,
            GeneratedAtUtc = DateTime.UtcNow,
            Model = _options.Model,
            Provider = _options.Provider
        };
    }

    private static SuggestedRetryAction GetFallbackAction(WebhookFailureAnalysisRequestDto request)
    {
        if (HasReachedMaxRetryCount(request))
        {
            return SuggestedRetryAction.MoveToDeadLetter;
        }

        return request.StatusCode switch
        {
            429 => SuggestedRetryAction.RetryWithBackoff,
            408 or 500 or 502 or 503 or 504 => SuggestedRetryAction.RetryWithBackoff,
            401 or 403 => SuggestedRetryAction.RequireManualReview,
            400 or 404 => SuggestedRetryAction.RequireManualReview,
            _ => SuggestedRetryAction.RequireManualReview
        };
    }

    private static AiRiskLevel GetFallbackRiskLevel(WebhookFailureAnalysisRequestDto request, SuggestedRetryAction action)
    {
        if (action == SuggestedRetryAction.MoveToDeadLetter)
        {
            return AiRiskLevel.High;
        }

        return request.StatusCode switch
        {
            401 or 403 => AiRiskLevel.High,
            400 or 404 => AiRiskLevel.Medium,
            429 or 500 or 502 or 503 or 504 or 408 => AiRiskLevel.Medium,
            _ => AiRiskLevel.Unknown
        };
    }

    private static void ApplyRetrySafetyRules(
        WebhookFailureAnalysisResponseDto response,
        WebhookFailureAnalysisRequestDto request)
    {
        if (HasReachedMaxRetryCount(request))
        {
            response.SuggestedRetryAction = SuggestedRetryAction.MoveToDeadLetter;
            response.IsRetryRecommended = false;
            response.AiRecommendation = AppendSafetyNote(response.AiRecommendation, "Max retry count has been reached; move the event to dead letter instead of retrying.");
            return;
        }

        if (request.StatusCode is 401 or 403)
        {
            response.SuggestedRetryAction = SuggestedRetryAction.RequireManualReview;
            response.IsRetryRecommended = false;
            response.AiRecommendation = AppendSafetyNote(response.AiRecommendation, "Authentication or authorization failures require manual review before retrying.");
            return;
        }

        if (request.StatusCode == 429 && response.SuggestedRetryAction == SuggestedRetryAction.RetryImmediately)
        {
            response.SuggestedRetryAction = SuggestedRetryAction.RetryWithBackoff;
            response.IsRetryRecommended = true;
            response.AiRecommendation = AppendSafetyNote(response.AiRecommendation, "Rate-limited requests must use backoff and must not be retried immediately.");
        }
    }

    private static bool HasReachedMaxRetryCount(WebhookFailureAnalysisRequestDto request)
        => request.MaxRetryCount > 0 && request.RetryCount >= request.MaxRetryCount;

    private static string BuildFallbackSummary(WebhookFailureAnalysisRequestDto request)
        => request.StatusCode is null
            ? "Rule-based analysis could not determine an HTTP status code for this failed webhook delivery."
            : $"Rule-based analysis evaluated failed webhook delivery status code {request.StatusCode}.";

    private static string BuildFallbackRootCause(WebhookFailureAnalysisRequestDto request)
        => request.StatusCode switch
        {
            429 => "The target endpoint reported rate limiting.",
            408 or 504 => "The delivery attempt timed out or the upstream gateway timed out.",
            500 or 502 or 503 => "The target endpoint or upstream service returned a transient server error.",
            401 or 403 => "The target endpoint rejected the request due to authentication or authorization.",
            400 or 404 => "The target endpoint rejected the request as a client-side or not-found error.",
            _ => "The failure cause is unknown from available status information."
        };

    private static string AppendSafetyNote(string recommendation, string note)
        => string.IsNullOrWhiteSpace(recommendation) ? note : $"{recommendation} Safety override: {note}";

    private static bool HasNonEmptyString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryGetEnum<TEnum>(JsonElement root, string propertyName, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Enum.TryParse(property.GetString(), ignoreCase: false, out value) &&
               Enum.IsDefined(typeof(TEnum), value);
    }

    private static double ClampConfidence(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 1);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
