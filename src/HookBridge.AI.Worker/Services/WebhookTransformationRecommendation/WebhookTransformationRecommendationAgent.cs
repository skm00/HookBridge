using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;

public sealed partial class WebhookTransformationRecommendationAgent : IWebhookTransformationRecommendationAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, string[]> Variants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = ["identifier"], ["identifier"] = ["id"],
        ["orderId"] = ["order_id", "orderid"], ["order_id"] = ["orderId", "orderid"],
        ["customerId"] = ["customer_id", "customerid"], ["customer_id"] = ["customerId", "customerid"],
        ["createdAt"] = ["created_at", "createdAtUtc", "created_at_utc"], ["created_at"] = ["createdAt", "createdAtUtc"],
        ["status"] = ["state", "order_status"], ["state"] = ["status"]
    };

    private readonly AiOptions _options;
    private readonly IWebhookTransformationPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<WebhookTransformationRecommendationAgent> _logger;

    public WebhookTransformationRecommendationAgent(IOptions<AiOptions> options, IWebhookTransformationPromptBuilder promptBuilder, ILocalLlmClient llmClient, ILogger<WebhookTransformationRecommendationAgent> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<WebhookTransformationRecommendationResponseDto> RecommendAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request, validateJson: false);

        if (!_options.Enabled)
            return CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic transformation recommendations were used.");

        if (!TryParseNode(request.SourcePayload, out _, out _))
            return CreateFallback(request, AiFallbackReason.InvalidJson, "SourcePayload is not valid JSON.");

        if (!HasValidTarget(request))
            return CreateFallback(request, AiFallbackReason.ConfigurationError, "TargetSchema/TargetSamplePayload is invalid or missing.");

        try
        {
            var llmResponse = await _llmClient.GenerateAsync(_promptBuilder.BuildPrompt(request), cancellationToken);
            if (!llmResponse.IsSuccess)
                return CreateFallback(request, llmResponse.FallbackReason, llmResponse.ErrorMessage);

            if (!TryParseResponse(llmResponse.ResponseText, request, out var response, out var failure))
            {
                _logger.LogWarning("Webhook transformation AI response was invalid. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}", request.EventId, request.CorrelationId, failure);
                return CreateFallback(request, AiFallbackReason.InvalidJson, $"AI response could not be used: {failure}");
            }

            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook transformation fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, ex.Message);
        }
    }

    private bool TryParseResponse(string responseText, WebhookTransformationRecommendationRequestDto request, out WebhookTransformationRecommendationResponseDto response, out string failure)
    {
        response = new(); failure = string.Empty;
        try { response = JsonSerializer.Deserialize<WebhookTransformationRecommendationResponseDto>(responseText, JsonOptions) ?? new(); }
        catch (JsonException ex) { failure = $"invalid JSON: {ex.Message}"; return false; }

        response.EventId = ValueOrDefault(response.EventId, request.EventId);
        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? request.CorrelationId : response.CorrelationId;
        response.Summary = ValueOrDefault(response.Summary, "Webhook transformation recommendation completed by AI.");
        response.RecommendedMappings = (response.RecommendedMappings ?? Array.Empty<WebhookFieldMappingRecommendationDto>()).Select(NormalizeMapping).ToArray();
        response.MissingTargetFields ??= Array.Empty<string>();
        response.UnmappedSourceFields ??= Array.Empty<string>();
        response.TransformationNotes ??= Array.Empty<string>();
        response.GeneratedTransformationCode = EnsureRecommendedCode(ValueOrDefault(response.GeneratedTransformationCode, GenerateTransformationCode(response.RecommendedMappings)));
        response.ConfidenceScore = Clamp(response.ConfidenceScore);
        response.RiskLevel = NormalizeRiskLevel(response.RiskLevel);
        response.GeneratedAtUtc = EnsureUtc(response.GeneratedAtUtc == default ? DateTime.UtcNow : response.GeneratedAtUtc);
        response.Model = _options.Model;
        response.Provider = _options.Provider;
        response.Fallback = new AiFallbackMetadataDto { UsedFallback = false, FallbackReason = AiFallbackReason.None, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = DateTime.UtcNow };
        ValidateResponse(response);
        return true;
    }

    private WebhookTransformationRecommendationResponseDto CreateFallback(WebhookTransformationRecommendationRequestDto request, AiFallbackReason reason, string? message)
    {
        var notes = new List<string> { "Fallback recommendations are deterministic and conservative; human review is required before production use." };
        if (!string.IsNullOrWhiteSpace(message)) notes.Add(message);

        var mappings = new List<WebhookFieldMappingRecommendationDto>();
        var missingTargets = new List<string>();
        var unmappedSources = new List<string>();
        var confidence = 0.25;

        if (TryParseNode(request.SourcePayload, out var sourceNode, out _) && TryGetTargetNode(request, out var targetNode))
        {
            var sourceFields = Flatten(sourceNode!).ToList();
            var targetFields = Flatten(targetNode!).ToList();
            var usedSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targetFields)
            {
                var source = FindMatch(target, sourceFields, usedSource);
                if (source is null)
                {
                    missingTargets.Add(target.Path);
                    continue;
                }

                usedSource.Add(source.Path);
                var exact = source.Name == target.Name;
                mappings.Add(new WebhookFieldMappingRecommendationDto
                {
                    SourceJsonPath = source.Path,
                    TargetJsonPath = target.Path,
                    SourceFieldName = source.Name,
                    TargetFieldName = target.Name,
                    TransformationType = exact ? WebhookTransformationType.DirectMap : WebhookTransformationType.Rename,
                    TransformationExpression = $"{target.Name} = {source.Name}",
                    IsRequired = true,
                    ConfidenceScore = exact ? 0.72 : 0.62,
                    Notes = exact ? "Exact field-name fallback match." : "Fallback field-name variant match."
                });
            }
            unmappedSources.AddRange(sourceFields.Where(s => !usedSource.Contains(s.Path)).Select(s => s.Path));
            confidence = mappings.Count > 0 ? 0.48 : 0.2;
        }
        else if (!TryParseNode(request.SourcePayload, out _, out _))
        {
            notes.Add("SourcePayload could not be parsed as JSON, so field mappings could not be inferred.");
        }
        else
        {
            notes.Add("No valid target schema or sample payload was available, so target-field mapping could not be inferred.");
        }

        var response = new WebhookTransformationRecommendationResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Summary = "Deterministic fallback generated conservative webhook transformation recommendations.",
            RecommendedMappings = mappings,
            MissingTargetFields = missingTargets,
            UnmappedSourceFields = unmappedSources,
            TransformationNotes = notes,
            GeneratedTransformationCode = GenerateTransformationCode(mappings),
            ConfidenceScore = confidence,
            RiskLevel = mappings.Count == 0 || missingTargets.Count > 0 ? "Medium" : "Low",
            GeneratedAtUtc = DateTime.UtcNow,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = new AiFallbackMetadataDto { UsedFallback = true, FallbackReason = reason, FallbackMessage = message ?? string.Empty, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = DateTime.UtcNow }
        };
        ValidateResponse(response);
        return response;
    }

    private static WebhookFieldMappingRecommendationDto NormalizeMapping(WebhookFieldMappingRecommendationDto mapping)
    {
        mapping.ConfidenceScore = Clamp(mapping.ConfidenceScore);
        mapping.SourceJsonPath ??= string.Empty; mapping.TargetJsonPath ??= string.Empty; mapping.SourceFieldName ??= string.Empty; mapping.TargetFieldName ??= string.Empty; mapping.TransformationExpression ??= string.Empty; mapping.Notes ??= string.Empty;
        return mapping;
    }

    private static FieldPath? FindMatch(FieldPath target, IReadOnlyList<FieldPath> sourceFields, HashSet<string> used)
    {
        return sourceFields.FirstOrDefault(s => !used.Contains(s.Path) && s.Name == target.Name)
            ?? sourceFields.FirstOrDefault(s => !used.Contains(s.Path) && string.Equals(s.Name, target.Name, StringComparison.OrdinalIgnoreCase))
            ?? sourceFields.FirstOrDefault(s => !used.Contains(s.Path) && AreVariants(s.Name, target.Name));
    }

    private static bool AreVariants(string source, string target)
    {
        var ns = NormalizeName(source); var nt = NormalizeName(target);
        if (ns == nt) return true;
        return (Variants.TryGetValue(source, out var sourceVariants) && sourceVariants.Any(v => string.Equals(NormalizeName(v), nt, StringComparison.OrdinalIgnoreCase)))
            || (Variants.TryGetValue(target, out var targetVariants) && targetVariants.Any(v => string.Equals(NormalizeName(v), ns, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeName(string value) => Regex.Replace(value, "[^A-Za-z0-9]", string.Empty).ToLowerInvariant();

    private static IEnumerable<FieldPath> Flatten(JsonNode node, string path = "$", string? name = null)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is null) continue;
                var childPath = $"{path}.{kvp.Key}";
                if (kvp.Value is JsonObject or JsonArray) foreach (var nested in Flatten(kvp.Value, childPath, kvp.Key)) yield return nested;
                else yield return new FieldPath(kvp.Key, childPath);
            }
        }
        else if (node is JsonArray arr && arr.Count > 0 && arr[0] is not null)
        {
            foreach (var nested in Flatten(arr[0]!, $"{path}[0]", name)) yield return nested;
        }
    }

    private static bool TryGetTargetNode(WebhookTransformationRecommendationRequestDto request, out JsonNode? target)
    {
        if (TryParseNode(request.TargetSamplePayload, out target, out _)) return true;
        if (TryParseNode(request.TargetSchema, out var schema, out _) && TryBuildSampleFromSchema(schema!, out target)) return true;
        target = null; return false;
    }

    private static bool TryBuildSampleFromSchema(JsonNode schema, out JsonNode? sample)
    {
        sample = null;
        var props = schema["properties"] as JsonObject ?? schema as JsonObject;
        if (props is null) return false;
        var obj = new JsonObject();
        foreach (var kvp in props) obj[kvp.Key] = "";
        sample = obj; return obj.Count > 0;
    }

    private static bool HasValidTarget(WebhookTransformationRecommendationRequestDto request) => TryGetTargetNode(request, out _);

    private static bool TryParseNode(object? payload, out JsonNode? node, out string? failure)
    {
        node = null; failure = null;
        try
        {
            var json = payload switch { null => string.Empty, string s => s, JsonElement e => e.GetRawText(), JsonNode n => n.ToJsonString(), _ => JsonSerializer.Serialize(payload) };
            if (string.IsNullOrWhiteSpace(json)) { failure = "JSON is empty."; return false; }
            node = JsonNode.Parse(json); return node is not null;
        }
        catch (JsonException ex) { failure = ex.Message; return false; }
    }

    private static string GenerateTransformationCode(IReadOnlyList<WebhookFieldMappingRecommendationDto> mappings)
    {
        var sb = new StringBuilder("// Recommended transformation code only. Requires human review before production use; HookBridge does not auto-apply this code.\n");
        sb.Append("using System.Text.Json.Nodes;\n\n");
        sb.Append("public static JsonObject Transform(JsonObject source)\n{\n    var target = new JsonObject();\n");
        foreach (var mapping in mappings.Where(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.TargetFieldName)))
        {
            sb.Append("    if (source.TryGetPropertyValue(\"").Append(Escape(mapping.SourceFieldName)).Append("\", out var ").Append(SafeVar(mapping.SourceFieldName)).Append("))\n");
            sb.Append("        target[\"").Append(Escape(mapping.TargetFieldName)).Append("\"] = ").Append(SafeVar(mapping.SourceFieldName)).Append("?.DeepClone();\n");
        }
        sb.Append("    return target;\n}\n");
        return sb.ToString();
    }

    private static string EnsureRecommendedCode(string code) => code.Contains("human review", StringComparison.OrdinalIgnoreCase) ? code : "// Recommended transformation code only. Requires human review before production use; HookBridge does not auto-apply this code.\n" + code;
    private static string SafeVar(string name) => "value_" + Regex.Replace(name, "[^A-Za-z0-9_]", "_");
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    private static string NormalizeRiskLevel(string? risk) => new[] { "Unknown", "Low", "Medium", "High", "Critical" }.FirstOrDefault(v => string.Equals(v, risk, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    private static string ValueOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static void ValidateRequest(WebhookTransformationRecommendationRequestDto request, bool validateJson = true)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) throw new ArgumentException("EventId is required.", nameof(request));
        if (request.SourcePayload is null || (request.SourcePayload is string s && string.IsNullOrWhiteSpace(s))) throw new ArgumentException("SourcePayload is required.", nameof(request));
        if (request.ReceivedAtUtc != default && request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
        if (validateJson && !TryParseNode(request.SourcePayload, out _, out _)) throw new ArgumentException("SourcePayload must be valid JSON.", nameof(request));
    }

    private static void ValidateResponse(WebhookTransformationRecommendationResponseDto response)
    {
        if (response.GeneratedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("GeneratedAtUtc must be UTC.", nameof(response));
        if (response.ConfidenceScore is < 0 or > 1) throw new ArgumentException("ConfidenceScore must be between 0 and 1.", nameof(response));
    }

    private sealed record FieldPath(string Name, string Path);
}
