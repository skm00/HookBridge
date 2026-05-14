using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Prompts;

public sealed partial class FluentValidationPromptBuilder : IFluentValidationPromptBuilder
{
    public const int MaxPayloadCharacters = 8_000;
    public const int MaxGeneratedDtoCodeCharacters = 12_000;
    private const string MaskedValue = "***MASKED***";
    private const string NotProvidedValue = "not provided";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key", "X-API-Key", "ClientSecret", "AccessToken", "ConnectionString"
    ];

    public string BuildPrompt(FluentValidationRuleGenerationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = Truncate(MaskSensitiveJson(SerializePayload(request.Payload)), MaxPayloadCharacters);
        var dtoCode = Truncate(MaskSensitiveText(request.GeneratedDtoCode ?? string.Empty), MaxGeneratedDtoCodeCharacters);
        var detectedFields = request.DetectedFields.Select(field => new
        {
            field.FieldName,
            field.JsonPath,
            field.InferredType,
            field.IsRequired,
            SampleValue = string.IsNullOrWhiteSpace(field.SampleValue) ? null : MaskSensitiveText(field.SampleValue),
            field.Description
        });
        var context = new
        {
            request.EventId,
            request.CorrelationId,
            request.EventType,
            request.Source,
            request.CustomerId,
            request.RootClassName,
            Namespace = ValueOrNotProvided(request.Namespace),
            request.RequiredFields,
            request.ReceivedAtUtc,
            Payload = payload,
            GeneratedDtoCode = dtoCode,
            DetectedFields = detectedFields
        };

        return $$"""
You are HookBridge AI, an assistant for generating FluentValidation validators for webhook DTOs.

Generate a safe C# FluentValidation validator for the supplied DTO and JSON payload.

Rules:
- Return strict JSON only. Do not include markdown, prose, comments, or code fences.
- Include generatedValidatorCode as one JSON string field containing all generated C# code.
- Use C# / .NET 8 compatible syntax and FluentValidation.
- The validator class name must be {{request.RootClassName}}Validator.
- The generated code must include: using FluentValidation;
- Use AbstractValidator<{{request.RootClassName}}>.
- Avoid inventing fields that are not present in the generated DTO code or payload.
- Use clear validation error messages.
- Add NotEmpty rules for required fields.
- Add MaximumLength rules for obvious string fields only where safe.
- Add GreaterThan or GreaterThanOrEqualTo rules for numeric quantity, count, amount, total, or price fields.
- Add EmailAddress rule for email fields.
- Add URL validation using Uri.TryCreate for URL/URI fields where safe.
- Add ISO/UTC date validation where applicable.
- Avoid database calls, HTTP calls, file access, randomness, time-dependent business logic, and side effects.
- Avoid business logic that cannot be inferred from the payload.
- Treat masked values as unavailable and never reconstruct secrets.
- Do not place secret sample values in generatedValidatorCode.
- If payload or DTO code is truncated, analyze only visible structure and include a validation note.
- confidenceScore must be a number from 0 to 1.
- generatedAtUtc must be UTC in ISO 8601 format.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "validatorClassName": "string",
  "namespace": "string or null",
  "generatedValidatorCode": "string",
  "rules": [
    {
      "propertyName": "string",
      "ruleType": "string",
      "ruleExpression": "string",
      "errorMessage": "string",
      "severity": "Info|Warning|Error|Critical",
      "description": "string"
    }
  ],
  "summary": "string",
  "validationNotes": [],
  "confidenceScore": 0.0,
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook validation context:
{{JsonSerializer.Serialize(context, JsonOptions)}}
""";
    }

    private static string SerializePayload(object? payload)
        => payload switch
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
            masked = JsonPropertyRegex(term).Replace(masked, match => $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
        }

        return masked;
    }

    private static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = SensitiveAssignmentRegex(term).Replace(masked, match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{MaskedValue}");
        }

        return AssignmentStringLiteralRegex().Replace(masked, match => $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
    }

    private static string ValueOrNotProvided(string? value) => string.IsNullOrWhiteSpace(value) ? NotProvidedValue : value;

    private static Regex JsonPropertyRegex(string term) => new(
        $"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static Regex SensitiveAssignmentRegex(string term) => new(
        $@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]"" ]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    [GeneratedRegex("(?<prefix>=\\s*\")[^\"]*(?<suffix>\")", RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentStringLiteralRegex();
}
