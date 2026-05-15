using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed partial class WebhookTransformationPromptBuilder : IWebhookTransformationPromptBuilder
{
    public const string MaskedValue = "[MASKED]";
    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key",
        "X-API-Key", "ClientSecret", "AccessToken", "ConnectionString"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly AiOptions _options;
    private readonly IAiPromptVersionProvider? _promptVersionProvider;

    public WebhookTransformationPromptBuilder(IOptions<AiOptions> options, IAiPromptVersionProvider? promptVersionProvider = null)
    {
        _options = options.Value;
        _promptVersionProvider = promptVersionProvider;
    }

    public string BuildPrompt(WebhookTransformationRecommendationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = Math.Max(1, _options.MaxPromptPayloadLength);
        var context = new
        {
            eventId = request.EventId,
            correlationId = request.CorrelationId,
            eventType = request.EventType,
            source = request.Source,
            customerId = request.CustomerId,
            sourcePayload = Truncate(MaskIfEnabled(Serialize(request.SourcePayload)), max),
            targetSchema = Truncate(MaskIfEnabled(Serialize(request.TargetSchema)), max),
            targetSamplePayload = Truncate(MaskIfEnabled(Serialize(request.TargetSamplePayload)), max),
            existingMappingRules = Truncate(MaskIfEnabled(Serialize(request.ExistingMappingRules)), max),
            headers = MaskHeaders(request.Headers ?? new Dictionary<string, string>()),
            receivedAtUtc = request.ReceivedAtUtc
        };

        return $$"""
You are HookBridge AI, an assistant that recommends webhook payload transformation rules.

Compare the source payload with the target schema and/or target sample payload. Recommend mappings only; do not auto-apply anything.
Rules:
- Return strict JSON only. Do not include markdown, prose, comments, or code fences.
- Use only source fields actually present in sourcePayload. Do not invent unavailable source fields.
- Mark missing required target fields clearly in missingTargetFields.
- Recommended mappings must use JSONPath-like paths such as $.order_id and $.orderId.
- transformationType must be one of DirectMap, Rename, TypeConversion, DateFormat, DefaultValue, CombineFields, SplitField, ConstantValue, Conditional, Ignore, Custom.
- generatedTransformationCode must be recommended C# / .NET 8 code using System.Text.Json.Nodes (JsonNode/JsonObject) or System.Text.Json.
- generatedTransformationCode must avoid paid dependencies, database calls, HTTP calls, file calls, side effects, and hardcoded secret/sample sensitive values.
- generatedTransformationCode must contain a clear warning comment that it is recommended code requiring human review and is not auto-applied production code.
- Treat masked values as unavailable. Never reconstruct or include secrets.
- confidenceScore and every mapping confidenceScore must be from 0 to 1.
- generatedAtUtc must be UTC ISO 8601.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

The JSON object must match this exact shape:
{
  "eventId": "string",
  "correlationId": "string or null",
  "summary": "string",
  "recommendedMappings": [
    {"sourceJsonPath":"string","targetJsonPath":"string","sourceFieldName":"string","targetFieldName":"string","transformationType":"DirectMap|Rename|TypeConversion|DateFormat|DefaultValue|CombineFields|SplitField|ConstantValue|Conditional|Ignore|Custom","transformationExpression":"string","defaultValue":null,"isRequired":true,"confidenceScore":0.0,"notes":"string"}
  ],
  "missingTargetFields": [],
  "unmappedSourceFields": [],
  "transformationNotes": [],
  "generatedTransformationCode": "string",
  "confidenceScore": 0.0,
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Webhook transformation context:
{{JsonSerializer.Serialize(context, JsonOptions)}}
""";
    }

    private string MaskIfEnabled(string value) => _options.MaskSensitiveValues ? MaskSensitiveText(value) : value;

    private static string Serialize(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(value, JsonOptions)
    };

    private static IDictionary<string, string> MaskHeaders(IDictionary<string, string> headers)
        => headers.ToDictionary(kvp => kvp.Key, kvp => IsSensitive(kvp.Key) ? MaskedValue : kvp.Value, StringComparer.OrdinalIgnoreCase);

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");

    public static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = new Regex($"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                .Replace(masked, m => $"{m.Groups["prefix"].Value}{MaskedValue}{m.Groups["suffix"].Value}");
            masked = new Regex($@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]"" ]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                .Replace(masked, m => $"{m.Groups["key"].Value}{m.Groups["separator"].Value}{MaskedValue}");
        }
        return masked;
    }

    private static bool IsSensitive(string key) => SensitiveTerms.Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase));

    public async Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default)
    {
        var content = BuildPrompt(request);
        var metadata = new AiPromptVersionInfoDto
        {
            PromptName = AiPromptNames.WebhookTransformationRecommendation,
            Version = AiPromptOptions.DefaultPromptVersion
        };

        if (_promptVersionProvider is not null)
        {
            metadata = (await _promptVersionProvider.GetPromptAsync(AiPromptNames.WebhookTransformationRecommendation, cancellationToken: cancellationToken)).Metadata;
        }

        return new AiPromptBuildResult { Content = content, Metadata = metadata };
    }

}
