using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class AiSecurityAnalysisPromptBuilder : IAiSecurityAnalysisPromptBuilder
{
    public const string MaskedValue = "[MASKED]";
    private const string NotProvidedValue = "[not provided]";

    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key",
        "X-API-Key", "ClientSecret", "client_secret", "AccessToken", "access_token", "Bearer"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly AiOptions _options;
    private readonly IAiPromptVersionProvider? _promptVersionProvider;

    public AiSecurityAnalysisPromptBuilder(IOptions<AiOptions> options, IAiPromptVersionProvider? promptVersionProvider = null)
    {
        _options = options.Value;
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(AiSecurityAnalysisRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payloadJson = SerializePayload(request.Payload);
        var maskedPayload = _options.MaskSensitiveValues ? MaskSensitiveText(payloadJson) : payloadJson;
        var payload = Truncate(maskedPayload, Math.Max(1, _options.MaxSecurityPayloadLength));
        var headers = MaskHeaders(request.Headers);

        var context = new
        {
            eventId = ValueOrNotProvided(request.EventId),
            correlationId = request.CorrelationId,
            customerId = request.CustomerId,
            customerIdType = request.CustomerIdType,
            subscriptionId = request.SubscriptionId,
            endpointId = request.EndpointId,
            environment = request.Environment,
            source = request.Source,
            eventType = ValueOrNotProvided(request.EventType),
            targetUrl = request.TargetUrl,
            httpMethod = request.HttpMethod,
            headers,
            payload,
            sourceIp = request.SourceIp,
            userAgent = request.UserAgent,
            signatureValidationFailed = request.SignatureValidationFailed,
            authenticationFailed = request.AuthenticationFailed,
            payloadSizeBytes = request.PayloadSizeBytes,
            largePayloadThresholdBytes = _options.LargePayloadThresholdBytes,
            receivedAtUtc = request.ReceivedAtUtc
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);
        return $$"""
You are HookBridge AI, an advisory security analysis assistant for webhook processing.

Analyze the webhook payload, headers, source metadata, and delivery context for suspicious security patterns. Check for: signature validation failure, authentication failure, very large payload, suspicious script content, SQL injection-like strings, command injection-like strings, path traversal patterns, unexpected binary/base64-heavy content, repeated token/secret-looking fields, unknown event type, dangerous URLs or callback URLs, header anomalies, unusual User-Agent, and missing required security headers if applicable.

Rules:
- Use only supplied context and visible payload/header evidence; do not invent missing evidence.
- Treat masked values as unavailable and never reconstruct secrets.
- If payload is truncated, analyze only visible content and lower confidence if important evidence may be hidden.
- Keep summary and recommendation short, actionable, and advisory.
- Do not recommend automatic permanent blocking of production traffic without human approval.
- securityRiskScore must be an integer from 0 to 100.
- confidenceScore must be a number from 0 to 1.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.
- suggestedAction must be one of None, Allow, Monitor, RequireManualReview, Quarantine, BlockTemporarily, Reject.

Return strict JSON only. Do not include markdown, prose, comments, or code fences.
The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "isSuspicious": true,
  "securityRiskScore": 0,
  "riskLevel": "Low|Medium|High|Critical|Unknown",
  "summary": "string",
  "recommendation": "string",
  "detectedSecuritySignals": [
    {
      "signalName": "string",
      "severity": "Low|Medium|High|Critical",
      "description": "string",
      "evidence": "string",
      "recommendation": "string"
    }
  ],
  "suggestedAction": "None|Allow|Monitor|RequireManualReview|Quarantine|BlockTemporarily|Reject",
  "confidenceScore": 0.0,
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook security context:
{{contextJson}}
""";
    }

    private IDictionary<string, string> MaskHeaders(IDictionary<string, string>? headers)
        => headers?.ToDictionary(pair => pair.Key, pair => IsSensitiveName(pair.Key) && _options.MaskSensitiveValues ? MaskedValue : MaskSensitiveText(pair.Value), StringComparer.OrdinalIgnoreCase)
           ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string SerializePayload(object? payload) => payload switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(payload, JsonOptions)
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");

    private static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = JsonPropertyRegex(term).Replace(masked, match => $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
            masked = SensitiveAssignmentRegex(term).Replace(masked, match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{MaskedValue}");
        }
        return masked;
    }

    private static bool IsSensitiveName(string name)
        => SensitiveTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string ValueOrNotProvided(string? value) => string.IsNullOrWhiteSpace(value) ? NotProvidedValue : value;

    private static Regex JsonPropertyRegex(string term) => new($"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static Regex SensitiveAssignmentRegex(string term) => new($@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]"" ]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(AiSecurityAnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.AiSecurityAnalysis,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.AiSecurityAnalysis, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
