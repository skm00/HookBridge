using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class JsonToDtoPromptBuilder : IJsonToDtoPromptBuilder
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

    public JsonToDtoPromptBuilder(IOptions<AiOptions> options, IAiPromptVersionProvider? promptVersionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(JsonToDtoSuggestionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payloadJson = SerializePayload(request.Payload);
        var maskedPayload = _options.MaskSensitiveValues ? MaskSensitiveJson(payloadJson) : payloadJson;
        var truncatedPayload = Truncate(maskedPayload, Math.Max(1, _options.MaxPromptPayloadLength));

        var context = new
        {
            eventId = ValueOrNotProvided(request.EventId),
            correlationId = ValueOrNotProvided(request.CorrelationId),
            eventType = ValueOrNotProvided(request.EventType),
            source = ValueOrNotProvided(request.Source),
            customerId = ValueOrNotProvided(request.CustomerId),
            rootClassName = ValueOrNotProvided(request.RootClassName),
            @namespace = ValueOrNotProvided(request.Namespace),
            receivedAtUtc = request.ReceivedAtUtc,
            payload = truncatedPayload
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);

        return $$"""
You are HookBridge AI, an assistant for generating integration DTO classes from webhook JSON payloads.

Generate clean C# DTO classes from the supplied JSON payload.

Rules:
- Return strict JSON only. Do not include markdown, prose, comments, or code fences.
- Include generatedCode as one JSON string field containing all generated C# code.
- Use C# 12 / .NET 8 compatible syntax.
- Use public sealed class consistently.
- Use nullable reference types correctly.
- Include using System.Text.Json.Serialization; in generatedCode.
- Use System.Text.Json attributes by default.
- Use PascalCase C# property names.
- Preserve original JSON property names with [JsonPropertyName("jsonName")].
- Represent arrays as List<T>.
- Represent unknown objects as JsonElement when type cannot be inferred.
- Avoid hardcoded sample values, methods, behavior, and business logic.
- Avoid inventing missing fields. Use only visible payload fields.
- Treat masked values as unavailable and never reconstruct secrets.
- Sensitive values must remain masked for Authorization, Cookie, Set-Cookie, Token, Secret, Password, Api-Key, X-API-Key, ClientSecret, and AccessToken.
- If payload is truncated, analyze only visible structure and include a validation note.
- confidenceScore must be a number from 0 to 1.
- generatedAtUtc must be UTC in ISO 8601 format.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "suggestedRootClassName": "string",
  "namespace": "string or null",
  "generatedCode": "string",
  "classes": [
    {
      "className": "string",
      "properties": [
        {
          "propertyName": "string",
          "jsonName": "string",
          "cSharpType": "string",
          "isNullable": true,
          "isRequired": true,
          "description": "string"
        }
      ],
      "description": "string"
    }
  ],
  "summary": "string",
  "validationNotes": [],
  "confidenceScore": 0.0,
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook payload context:
{{contextJson}}
""";
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
        => value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");

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

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(JsonToDtoSuggestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.JsonToDtoSuggestion,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.JsonToDtoSuggestion, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
