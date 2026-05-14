using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class FluentValidationPromptBuilder : IFluentValidationPromptBuilder
{
    public const string MaskedValue = "[MASKED]";
    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key",
        "X-API-Key", "ClientSecret", "AccessToken", "ConnectionString"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly AiOptions _options;

    public FluentValidationPromptBuilder(IOptions<AiOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPrompt(FluentValidationRuleGenerationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payloadJson = SerializePayload(request.Payload);
        var dtoCode = request.GeneratedDtoCode ?? string.Empty;
        var max = Math.Max(1, _options.MaxPromptPayloadLength);
        var maskedPayload = _options.MaskSensitiveValues ? MaskSensitiveJson(payloadJson) : payloadJson;
        var maskedDtoCode = _options.MaskSensitiveValues ? MaskSensitiveText(dtoCode) : dtoCode;
        var context = new
        {
            eventId = request.EventId,
            correlationId = request.CorrelationId,
            eventType = request.EventType,
            source = request.Source,
            customerId = request.CustomerId,
            rootClassName = request.RootClassName,
            @namespace = request.Namespace,
            payload = Truncate(maskedPayload, max),
            generatedDtoCode = Truncate(maskedDtoCode, max),
            detectedFields = request.DetectedFields,
            requiredFields = request.RequiredFields,
            receivedAtUtc = request.ReceivedAtUtc
        };

        return $$"""
You are HookBridge AI, an assistant for generating FluentValidation validators for webhook DTOs.

Generate one validator for the supplied DTO and payload.
Rules:
- Return strict JSON only. Do not include markdown, prose, comments, or code fences.
- Include generatedValidatorCode as a JSON string field containing all C# validator code.
- Use FluentValidation and .NET 8 compatible C#.
- The validator class name must be {{request.RootClassName}}Validator.
- Use AbstractValidator<{{request.RootClassName}}> and include using FluentValidation;.
- Avoid inventing fields not present in the DTO, payload, detectedFields, or requiredFields.
- Add NotEmpty rules for required fields.
- Add MaximumLength rules only for obvious string fields where safe.
- Add GreaterThan or GreaterThanOrEqualTo rules for numeric quantity, count, amount, total, price, or cost fields.
- Add EmailAddress rules for email fields.
- Add absolute URI validation for URL/URI fields where safe.
- Add ISO/UTC date validation where applicable.
- Use clear validation error messages.
- Do not add database calls, HTTP calls, side effects, or uninferable business logic.
- Do not include sample values or sensitive values in generatedValidatorCode.
- Treat masked values as unavailable and never reconstruct secrets.
- confidenceScore must be from 0 to 1; generatedAtUtc must be UTC ISO 8601.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

The JSON object must match this exact shape:
{
  "eventId": "string",
  "correlationId": "string or null",
  "validatorClassName": "string",
  "namespace": "string or null",
  "generatedValidatorCode": "string",
  "rules": [
    {"propertyName":"string","ruleType":"string","ruleExpression":"string","errorMessage":"string","severity":"Info|Warning|Error|Critical","description":"string"}
  ],
  "summary": "string",
  "validationNotes": [],
  "confidenceScore": 0.0,
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook DTO and payload context:
{{JsonSerializer.Serialize(context, JsonOptions)}}
""";
    }

    private static string SerializePayload(object? payload) => payload switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(payload, JsonOptions)
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");

    private static string MaskSensitiveJson(string value)
    {
        var masked = MaskSensitiveText(value);
        foreach (var term in SensitiveTerms)
        {
            masked = new Regex($"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                .Replace(masked, m => $"{m.Groups["prefix"].Value}{MaskedValue}{m.Groups["suffix"].Value}");
        }
        return masked;
    }

    private static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = new Regex($@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]"" ]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                .Replace(masked, m => $"{m.Groups["key"].Value}{m.Groups["separator"].Value}{MaskedValue}");
        }
        return string.Join('\n', masked.Split('\n').Select(line => SensitiveTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase))
            ? Regex.Replace(line, "\"[^\"]*\"", $"\"{MaskedValue}\"")
            : line));
    }
}
