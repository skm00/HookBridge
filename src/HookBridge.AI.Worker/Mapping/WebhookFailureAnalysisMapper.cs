using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Mapping;

public static class WebhookFailureAnalysisMapper
{
    public static WebhookFailureAnalysisRequestDto ToWebhookFailureAnalysisRequest(
        AiAnalysisEventDto analysisEvent)
    {
        ArgumentNullException.ThrowIfNull(analysisEvent);

        var request = new WebhookFailureAnalysisRequestDto
        {
            EventId = analysisEvent.EventId,
            CorrelationId = analysisEvent.CorrelationId,
            EventType = analysisEvent.EventType,
            Source = analysisEvent.Source,
            FailureReason = analysisEvent.FailureReason,
            RequestPayload = analysisEvent.Payload,
            FailedAtUtc = analysisEvent.CreatedAtUtc.UtcDateTime
        };

        ApplyPayloadHints(request, analysisEvent.Payload);
        return request;
    }

    public static AiAnalysisResult ToAiAnalysisResult(
        WebhookFailureAnalysisResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AiAnalysisResult
        {
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            AiSummary = response.AiSummary,
            RootCause = response.RootCause,
            AiRecommendation = response.AiRecommendation,
            RiskLevel = response.RiskLevel.ToString(),
            ConfidenceScore = response.ConfidenceScore,
            SuggestedRetryAction = response.SuggestedRetryAction.ToString(),
            IsRetryRecommended = response.IsRetryRecommended,
            Model = response.Model,
            Provider = response.Provider,
            PromptName = response.PromptName,
            PromptVersion = response.PromptVersion,
            PromptHash = response.PromptHash,
            CreatedAtUtc = EnsureUtc(response.GeneratedAtUtc)
        };
    }

    public static AiAnalysisResult ToAiAnalysisResult(
        WebhookFailureAnalysisResponseDto response,
        WebhookFailureAnalysisRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);

        var result = ToAiAnalysisResult(response);
        result.Source = request.Source ?? string.Empty;
        result.EventType = request.EventType;
        result.FailureReason = request.FailureReason;
        return result;
    }

    public static AiAnalysisResult ToAiAnalysisResultPlaceholder(
        WebhookFailureAnalysisRequestDto request,
        AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        return new AiAnalysisResult
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Source = request.Source ?? string.Empty,
            EventType = request.EventType,
            FailureReason = request.FailureReason,
            AiSummary = string.IsNullOrWhiteSpace(request.FailureReason)
                ? "AI analysis placeholder created for webhook failure analysis."
                : $"AI analysis placeholder created for failure: {request.FailureReason}",
            RootCause = string.Empty,
            AiRecommendation = "Review the webhook payload, delivery history, and target endpoint health before retrying.",
            RiskLevel = AiRiskLevel.Unknown.ToString(),
            ConfidenceScore = 0,
            SuggestedRetryAction = SuggestedRetryAction.RequireManualReview.ToString(),
            IsRetryRecommended = false,
            Model = options.Model,
            Provider = options.Provider,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static void ApplyPayloadHints(WebhookFailureAnalysisRequestDto request, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            request.SubscriptionId ??= GetString(root, "subscriptionId");
            request.CustomerId ??= GetString(root, "customerId");
            request.CustomerIdType ??= GetString(root, "customerIdType");
            request.TargetUrl ??= GetString(root, "targetUrl");
            request.HttpMethod ??= GetString(root, "httpMethod");
            request.ErrorMessage ??= GetString(root, "errorMessage");
            request.FailureReason ??= GetString(root, "failureReason");
            request.StatusCode ??= GetInt32(root, "statusCode");
            request.RetryCount = GetInt32(root, "retryCount") ?? request.RetryCount;
            request.MaxRetryCount = GetInt32(root, "maxRetryCount") ?? request.MaxRetryCount;
        }
        catch (JsonException)
        {
            // Payload is optional context. Invalid JSON should not prevent analysis of the envelope fields.
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
