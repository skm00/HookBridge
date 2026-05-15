using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class PayloadSchemaDetectionPromptBuilder : IPayloadSchemaDetectionPromptBuilder
{
    public const string MaskedValue = "[MASKED]";
    private const string NotProvidedValue = "[not provided]";

    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key",
        "X-API-Key", "ClientSecret", "AccessToken"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly AiOptions _options;
    private readonly IAiPromptVersionProvider? _promptVersionProvider;

    public PayloadSchemaDetectionPromptBuilder(IOptions<AiOptions> options, IAiPromptVersionProvider? promptVersionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(PayloadSchemaDetectionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payloadJson = SerializePayload(request.Payload);
        var maskedPayload = _options.MaskSensitiveValues ? MaskSensitiveJson(payloadJson) : payloadJson;
        var truncatedPayload = Truncate(maskedPayload, Math.Max(1, _options.MaxPromptPayloadLength));
        var maskedHeaders = MaskHeaders(request.Headers);

        var context = new
        {
            eventId = ValueOrNotProvided(request.EventId),
            correlationId = ValueOrNotProvided(request.CorrelationId),
            source = ValueOrNotProvided(request.Source),
            eventType = ValueOrNotProvided(request.EventType),
            customerId = ValueOrNotProvided(request.CustomerId),
            receivedAtUtc = request.ReceivedAtUtc,
            headers = maskedHeaders,
            payload = truncatedPayload
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);

        return $$"""
You are HookBridge AI, an assistant for webhook payload schema detection.

Inspect the JSON payload structure and infer the likely schema, event type, important fields, missing fields, validation issues, DTO name, risk level, and confidence.

Rules:
- Use only fields that are present in the supplied JSON payload or explicitly provided context; do not invent fields.
- Treat masked values as unavailable and never reconstruct secrets.
- Sensitive values must remain masked for Authorization, Cookie, Set-Cookie, Token, Secret, Password, Api-Key, X-API-Key, ClientSecret, and AccessToken.
- If the payload is truncated, analyze only the visible structure and mention truncation as a validation issue when it affects confidence.
- Include confidenceScore as a number from 0 to 1.
- generatedAtUtc must be UTC in ISO 8601 format.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

Return strict JSON only. Do not include markdown, prose, comments, or code fences.
The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "detectedSchemaName": "string",
  "detectedEventType": "string",
  "summary": "string",
  "importantFields": [
    {
      "fieldName": "string",
      "jsonPath": "string",
      "inferredType": "string",
      "isRequired": true,
      "sampleValue": "string or null",
      "description": "string"
    }
  ],
  "missingFields": [],
  "validationIssues": [],
  "suggestedDtoName": "string",
  "confidenceScore": 0.0,
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook payload context:
{{contextJson}}
""";
    }

    private IDictionary<string, string> MaskHeaders(IDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return headers.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveName(pair.Key) && _options.MaskSensitiveValues ? MaskedValue : MaskSensitiveText(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string SerializePayload(object? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        if (payload is string text)
        {
            return text;
        }

        if (payload is JsonElement element)
        {
            return element.GetRawText();
        }

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");
    }

    private static string MaskSensitiveJson(string value)
    {
        var masked = MaskSensitiveText(value);

        foreach (var term in SensitiveTerms)
        {
            masked = JsonPropertyRegex(term).Replace(masked, match =>
                $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
        }

        return masked;
    }

    private static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = SensitiveAssignmentRegex(term).Replace(masked, match =>
                $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{MaskedValue}");
        }

        return masked;
    }

    private static bool IsSensitiveName(string name)
        => SensitiveTerms.Any(term => string.Equals(term, name, StringComparison.OrdinalIgnoreCase) ||
                                      name.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string ValueOrNotProvided(string? value)
        => string.IsNullOrWhiteSpace(value) ? NotProvidedValue : value;

    private static Regex JsonPropertyRegex(string term)
        => new(
            $"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static Regex SensitiveAssignmentRegex(string term)
        => new(
            $@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]"" ]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(PayloadSchemaDetectionRequestDto request, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.PayloadSchemaDetection,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.PayloadSchemaDetection, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
