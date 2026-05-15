using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class WebhookFailurePromptBuilder : IWebhookFailurePromptBuilder
{
    private const string MaskedValue = "[MASKED]";
    private const string NotProvidedValue = "[not provided]";

    private static readonly string[] SensitiveHeaderFragments =
    [
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-API-Key",
        "Api-Key",
        "Token",
        "Secret",
        "Password"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AiOptions _options;
    private readonly IAiPromptVersionProvider? _promptVersionProvider;

    public WebhookFailurePromptBuilder(IOptions<AiOptions> options, IAiPromptVersionProvider? promptVersionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(WebhookFailureAnalysisRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new
        {
            eventId = ValueOrNotProvided(request.EventId),
            correlationId = ValueOrNotProvided(request.CorrelationId),
            subscriptionId = ValueOrNotProvided(request.SubscriptionId),
            customerId = ValueOrNotProvided(request.CustomerId),
            customerIdType = ValueOrNotProvided(request.CustomerIdType),
            eventType = ValueOrNotProvided(request.EventType),
            source = ValueOrNotProvided(request.Source),
            targetUrl = ValueOrNotProvided(request.TargetUrl),
            httpMethod = ValueOrNotProvided(request.HttpMethod),
            statusCode = request.StatusCode,
            errorMessage = ValueOrNotProvided(request.ErrorMessage),
            failureReason = ValueOrNotProvided(request.FailureReason),
            retryCount = request.RetryCount,
            maxRetryCount = request.MaxRetryCount,
            failedAtUtc = request.FailedAtUtc,
            requestHeaders = SanitizeHeaders(request.RequestHeaders),
            responseHeaders = SanitizeHeaders(request.ResponseHeaders),
            requestPayload = Truncate(request.RequestPayload),
            responseBody = Truncate(request.ResponseBody)
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);
        var riskLevels = string.Join(", ", Enum.GetNames<AiRiskLevel>());
        var retryActions = string.Join(", ", Enum.GetNames<SuggestedRetryAction>());

        return $$"""
You are HookBridge AI, an assistant for webhook delivery failure analysis.

Analyze the webhook failure context below and produce a concise operational explanation for the failed delivery.

Required analysis focus:
- HTTP status code
- Error message
- Failure reason
- Retry count and max retry count
- Target URL
- Event type
- Request and response context
- Whether retry is safe
- Whether manual review is required

Safety and data handling rules:
- Do not invent missing data. If a field is missing, null, or marked {{NotProvidedValue}}, say it is not provided.
- Do not expose secrets or sensitive data.
- Treat sensitive headers as masked and never reconstruct their original values.
- Mask sensitive headers, including Authorization, Cookie, Set-Cookie, X-API-Key, Api-Key, Token, Secret, and Password.
- Keep aiRecommendation short and actionable.
- Use riskLevel only from: {{riskLevels}}.
- Use suggestedRetryAction only from: {{retryActions}}.
- confidenceScore must be a number between 0 and 1.
- generatedAtUtc must be the UTC time when you generate the analysis in ISO 8601 format.

Return strict JSON only. Do not include markdown, prose, comments, or code fences.
The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "aiSummary": "string",
  "rootCause": "string",
  "aiRecommendation": "string",
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "confidenceScore": 0.0,
  "suggestedRetryAction": "None|RetryImmediately|RetryWithBackoff|MoveToDeadLetter|PauseEndpoint|RequireManualReview",
  "isRetryRecommended": false,
  "generatedAtUtc": "2026-05-13T00:00:00Z"
}

Webhook failure context:
{{contextJson}}
""";
    }

    private static string ValueOrNotProvided(string? value)
        => string.IsNullOrWhiteSpace(value) ? NotProvidedValue : value;

    private IReadOnlyDictionary<string, string> SanitizeHeaders(Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var sanitized = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var value = _options.MaskSensitiveValues && IsSensitiveHeader(header.Key)
                ? MaskedValue
                : ValueOrNotProvided(header.Value);

            sanitized[header.Key] = Truncate(value);
        }

        return sanitized;
    }

    private static bool IsSensitiveHeader(string headerName)
        => SensitiveHeaderFragments.Any(fragment => headerName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private string Truncate(string? value)
    {
        var normalizedValue = ValueOrNotProvided(value);
        var maxLength = Math.Max(1, _options.MaxPromptPayloadLength);

        if (normalizedValue.Length <= maxLength)
        {
            return normalizedValue;
        }

        return string.Concat(
            normalizedValue.AsSpan(0, maxLength),
            $"... [truncated from {normalizedValue.Length} to {maxLength} characters]");
    }

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(WebhookFailureAnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.WebhookFailureAnalysis,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.WebhookFailureAnalysis, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
