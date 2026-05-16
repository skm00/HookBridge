using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.TransformationAgent;

public sealed partial class TransformationAgent : ITransformationAgent
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = ["identifier"],
        ["identifier"] = ["id"],
        ["orderId"] = ["order_id"],
        ["order_id"] = ["orderId"],
        ["customerId"] = ["customer_id"],
        ["customer_id"] = ["customerId"],
        ["createdAt"] = ["created_at", "createdAtUtc"],
        ["created_at"] = ["createdAt", "createdAtUtc"],
        ["createdAtUtc"] = ["createdAt", "created_at"],
        ["status"] = ["state", "order_status"],
        ["state"] = ["status"],
        ["order_status"] = ["status"],
        ["amount"] = ["totalAmount", "total_amount"],
        ["totalAmount"] = ["amount", "total_amount"],
        ["total_amount"] = ["amount", "totalAmount"]
    };

    private readonly TransformationAgentOptions _options;
    private readonly IWebhookTransformationRecommendationAgent _recommendationAgent;
    private readonly ILogger<TransformationAgent> _logger;

    public TransformationAgent(IOptions<TransformationAgentOptions> options, IWebhookTransformationRecommendationAgent recommendationAgent, ILogger<TransformationAgent> logger)
    {
        _options = options.Value;
        _recommendationAgent = recommendationAgent;
        _logger = logger;
    }

    public async Task<TransformationAgentResponseDto> AnalyzeAsync(TransformationAgentRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Transformation agent started. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, SubscriptionId: {SubscriptionId}, EndpointId: {EndpointId}, Environment: {Environment}", request.EventId, request.CorrelationId, request.CustomerId, request.SubscriptionId, request.EndpointId, request.Environment);

        var validationResults = request.Validate(new ValidationContext(request)).ToArray();
        if (validationResults.Length > 0 && validationResults.Any(result => result.MemberNames.Contains(nameof(TransformationAgentRequestDto.EventId)) || result.MemberNames.Contains(nameof(TransformationAgentRequestDto.ReceivedAtUtc)) || result.MemberNames.Contains(nameof(TransformationAgentRequestDto.SourcePayload))))
        {
            _logger.LogWarning("Invalid transformation agent request. EventId: {EventId}, CorrelationId: {CorrelationId}, ValidationErrorCount: {ValidationErrorCount}", request.EventId, request.CorrelationId, validationResults.Length);
            if (validationResults.Any(result => result.MemberNames.Contains(nameof(TransformationAgentRequestDto.SourcePayload)) && result.ErrorMessage?.Contains("valid JSON", StringComparison.OrdinalIgnoreCase) == true))
            {
                return Invalid(request, TransformationAgentDecision.InvalidSourcePayload, TransformationAgentReasonCode.InvalidSourceJson, "Source payload is not valid JSON.");
            }
            throw new ValidationException(validationResults[0].ErrorMessage);
        }

        if (!TryParseJson(request.SourcePayload, out var source, out _))
        {
            _logger.LogWarning("Invalid request. Source JSON parsing failed. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return Invalid(request, TransformationAgentDecision.InvalidSourcePayload, TransformationAgentReasonCode.InvalidSourceJson, "Source payload is not valid JSON.");
        }

        if (!TryGetTargetNode(request, out var target, out _))
        {
            _logger.LogWarning("Invalid request. Target schema/sample parsing failed. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return Invalid(request, TransformationAgentDecision.InvalidTargetSchema, TransformationAgentReasonCode.InvalidTargetJson, "Target schema or sample payload is missing or invalid.");
        }

        _logger.LogInformation("Payload/schema validation completed. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);

        TransformationAgentResponseDto response;
        if (_options.Enabled)
        {
            try
            {
                var aiResponse = await _recommendationAgent.RecommendAsync(new WebhookTransformationRecommendationRequestDto
                {
                    EventId = request.EventId,
                    CorrelationId = request.CorrelationId,
                    EventType = request.EventType,
                    Source = request.Source,
                    CustomerId = request.CustomerId,
                    SourcePayload = Truncate(request.SourcePayload, _options.MaxPayloadLength),
                    TargetSchema = Truncate(request.TargetSchema, _options.MaxSchemaLength),
                    TargetSamplePayload = Truncate(request.TargetSamplePayload, _options.MaxSchemaLength),
                    ExistingMappingRules = Truncate(request.ExistingMappingRules, _options.MaxSchemaLength),
                    ReceivedAtUtc = request.ReceivedAtUtc
                }, cancellationToken);
                response = FromRecommendation(request, aiResponse);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Transformation recommendation agent unavailable; deterministic fallback will be used. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
                response = CreateFallback(request, source!, target!);
            }
        }
        else
        {
            response = CreateFallback(request, source!, target!);
        }

        ApplyDecisionAndSafetyRules(response);
        _logger.LogInformation("Mapping recommendation calculated. EventId: {EventId}, CorrelationId: {CorrelationId}, TransformationDecision: {TransformationDecision}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}, ConfidenceScore: {ConfidenceScore}, Fallback: {Fallback}", response.EventId, response.CorrelationId, response.TransformationDecision, response.RiskLevel, response.RequiresApproval, response.ConfidenceScore, response.Fallback);
        if (response.MissingTargetFields.Count > 0) _logger.LogInformation("Missing target fields detected. EventId: {EventId}, CorrelationId: {CorrelationId}, MissingTargetFieldCount: {MissingTargetFieldCount}", response.EventId, response.CorrelationId, response.MissingTargetFields.Count);
        if (response.RequiresApproval) _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, TransformationDecision: {TransformationDecision}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.TransformationDecision, response.RiskLevel);
        return response;
    }

    private TransformationAgentResponseDto CreateFallback(TransformationAgentRequestDto request, JsonNode source, JsonNode target)
    {
        var sourceFields = Flatten(source).ToList();
        var targetFields = Flatten(target).ToList();
        var usedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new List<WebhookFieldMappingRecommendationDto>();
        var reasonCodes = new HashSet<TransformationAgentReasonCode>();

        foreach (var targetField in targetFields)
        {
            var match = FindMatch(targetField, sourceFields, usedSources, out var reasonCode);
            if (match is null) continue;
            usedSources.Add(match.Path);
            reasonCodes.Add(reasonCode);
            mappings.Add(new WebhookFieldMappingRecommendationDto
            {
                SourceJsonPath = match.Path,
                TargetJsonPath = targetField.Path,
                SourceFieldName = match.Name,
                TargetFieldName = targetField.Name,
                TransformationType = reasonCode == TransformationAgentReasonCode.DirectMappingAvailable ? WebhookTransformationType.DirectMap : WebhookTransformationType.Rename,
                IsRequired = true,
                ConfidenceScore = reasonCode == TransformationAgentReasonCode.DirectMappingAvailable ? 1.0 : 0.9,
                Notes = "Deterministic fallback recommendation; review before production use."
            });
        }

        var missingTargets = targetFields.Where(targetField => mappings.All(mapping => !string.Equals(mapping.TargetJsonPath, targetField.Path, StringComparison.OrdinalIgnoreCase))).Select(field => field.Path).ToArray();
        var unmappedSources = sourceFields.Where(sourceField => !usedSources.Contains(sourceField.Path)).Select(field => field.Path).ToArray();
        if (missingTargets.Length > 0) reasonCodes.Add(TransformationAgentReasonCode.MissingRequiredTargetField);
        if (unmappedSources.Any(ContainsImportantFieldName)) reasonCodes.Add(TransformationAgentReasonCode.UnmappedImportantSourceField);

        var confidence = CalculateConfidence(mappings, missingTargets, unmappedSources);
        return new TransformationAgentResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            RiskLevel = missingTargets.Length > 0 || unmappedSources.Any(ContainsImportantFieldName) ? "Medium" : "Low",
            RequiresApproval = false,
            Summary = missingTargets.Length == 0 ? "Deterministic fallback found mappings for the target fields." : "Deterministic fallback found missing target fields that require review.",
            Recommendation = "Review transformation recommendations before applying them to production; HookBridge never auto-applies generated transformations.",
            RecommendedMappings = mappings,
            MissingTargetFields = missingTargets,
            UnmappedSourceFields = unmappedSources,
            GeneratedTransformationCode = string.Empty,
            ReasonCodes = reasonCodes.Count == 0 ? [TransformationAgentReasonCode.Unknown] : reasonCodes.ToList(),
            ConfidenceScore = confidence,
            GeneratedAtUtc = DateTime.UtcNow,
            Fallback = true
        };
    }

    private TransformationAgentResponseDto FromRecommendation(TransformationAgentRequestDto request, WebhookTransformationRecommendationResponseDto ai) => new()
    {
        EventId = string.IsNullOrWhiteSpace(ai.EventId) ? request.EventId : ai.EventId,
        CorrelationId = string.IsNullOrWhiteSpace(ai.CorrelationId) ? request.CorrelationId : ai.CorrelationId,
        RiskLevel = NormalizeRisk(ai.RiskLevel),
        Summary = ai.Summary,
        Recommendation = "Generated transformation code is recommendation only and must be reviewed before production use.",
        RecommendedMappings = ai.RecommendedMappings,
        MissingTargetFields = ai.MissingTargetFields,
        UnmappedSourceFields = ai.UnmappedSourceFields,
        GeneratedTransformationCode = RemoveSecretLiterals(ai.GeneratedTransformationCode),
        ReasonCodes = BuildReasonCodes(ai),
        ConfidenceScore = Clamp(ai.ConfidenceScore),
        GeneratedAtUtc = EnsureUtc(ai.GeneratedAtUtc == default ? DateTime.UtcNow : ai.GeneratedAtUtc),
        Fallback = ai.Fallback?.UsedFallback ?? false,
        PromptName = ai.PromptName,
        PromptVersion = ai.PromptVersion,
        PromptHash = ai.PromptHash
    };

    private void ApplyDecisionAndSafetyRules(TransformationAgentResponseDto response)
    {
        response.ConfidenceScore = Clamp(response.ConfidenceScore);
        response.GeneratedAtUtc = EnsureUtc(response.GeneratedAtUtc == default ? DateTime.UtcNow : response.GeneratedAtUtc);
        response.RiskLevel = NormalizeRisk(response.RiskLevel);

        if (!string.IsNullOrWhiteSpace(response.GeneratedTransformationCode) && _options.RequireApprovalForGeneratedCode)
        {
            response.RequiresApproval = true;
            AddReason(response, TransformationAgentReasonCode.GeneratedCodeRequiresApproval);
        }
        if (response.RiskLevel == "High" && _options.RequireApprovalForHighRisk) response.RequiresApproval = true;
        if (response.RiskLevel == "Critical" && _options.RequireApprovalForCriticalRisk) response.RequiresApproval = true;
        if (response.MissingTargetFields.Count > 0)
        {
            response.RequiresApproval = true;
            response.TransformationDecision = TransformationAgentDecision.MissingRequiredFields;
            AddReason(response, TransformationAgentReasonCode.MissingRequiredTargetField);
            AddReason(response, TransformationAgentReasonCode.ManualReviewRequired);
            return;
        }
        if (response.ConfidenceScore < _options.MinimumReviewConfidenceScore)
        {
            response.TransformationDecision = TransformationAgentDecision.MappingNeedsReview;
            AddReason(response, TransformationAgentReasonCode.LowConfidenceMapping);
            return;
        }
        response.TransformationDecision = response.ConfidenceScore >= _options.MinimumReadyConfidenceScore
            ? TransformationAgentDecision.MappingReady
            : TransformationAgentDecision.MappingNeedsReview;
    }

    private TransformationAgentResponseDto Invalid(TransformationAgentRequestDto request, TransformationAgentDecision decision, TransformationAgentReasonCode reasonCode, string summary) => new()
    {
        EventId = request.EventId,
        CorrelationId = request.CorrelationId,
        TransformationDecision = decision,
        RiskLevel = "High",
        RequiresApproval = true,
        Summary = summary,
        Recommendation = "Fix the invalid transformation input and submit it for manual review.",
        ReasonCodes = [reasonCode, TransformationAgentReasonCode.ManualReviewRequired],
        ConfidenceScore = 0,
        GeneratedAtUtc = DateTime.UtcNow,
        Fallback = true
    };

    private static List<TransformationAgentReasonCode> BuildReasonCodes(WebhookTransformationRecommendationResponseDto response)
    {
        var codes = new HashSet<TransformationAgentReasonCode>();
        foreach (var mapping in response.RecommendedMappings)
        {
            codes.Add(mapping.TransformationType == WebhookTransformationType.DirectMap ? TransformationAgentReasonCode.DirectMappingAvailable : TransformationAgentReasonCode.RenameMappingAvailable);
            if (mapping.TransformationType is WebhookTransformationType.TypeConversion) codes.Add(TransformationAgentReasonCode.TypeConversionRequired);
            if (mapping.TransformationType is WebhookTransformationType.DateFormat) codes.Add(TransformationAgentReasonCode.DateFormatConversionRequired);
            if (mapping.ConfidenceScore < 0.60) codes.Add(TransformationAgentReasonCode.LowConfidenceMapping);
        }
        if (response.MissingTargetFields.Count > 0) codes.Add(TransformationAgentReasonCode.MissingRequiredTargetField);
        if (response.UnmappedSourceFields.Any(ContainsImportantFieldName)) codes.Add(TransformationAgentReasonCode.UnmappedImportantSourceField);
        return codes.Count == 0 ? [TransformationAgentReasonCode.Unknown] : codes.ToList();
    }

    private static FieldPath? FindMatch(FieldPath target, IReadOnlyList<FieldPath> sources, ISet<string> used, out TransformationAgentReasonCode reasonCode)
    {
        reasonCode = TransformationAgentReasonCode.DirectMappingAvailable;
        var exact = sources.FirstOrDefault(source => !used.Contains(source.Path) && source.Name == target.Name);
        if (exact is not null) return exact;
        var caseInsensitive = sources.FirstOrDefault(source => !used.Contains(source.Path) && string.Equals(source.Name, target.Name, StringComparison.OrdinalIgnoreCase));
        if (caseInsensitive is not null) return caseInsensitive;
        reasonCode = TransformationAgentReasonCode.RenameMappingAvailable;
        return sources.FirstOrDefault(source => !used.Contains(source.Path) && NormalizeName(source.Name) == NormalizeName(target.Name))
            ?? sources.FirstOrDefault(source => !used.Contains(source.Path) && IsAlias(source.Name, target.Name));
    }

    private static bool IsAlias(string source, string target)
        => Aliases.TryGetValue(source, out var aliases) && aliases.Any(alias => string.Equals(alias, target, StringComparison.OrdinalIgnoreCase))
           || Aliases.TryGetValue(target, out aliases) && aliases.Any(alias => string.Equals(alias, source, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<FieldPath> Flatten(JsonNode node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is null) continue;
                var childPath = $"{path}.{kvp.Key}";
                if (kvp.Value is JsonObject or JsonArray) foreach (var nested in Flatten(kvp.Value, childPath)) yield return nested;
                else yield return new FieldPath(kvp.Key, childPath);
            }
        }
        else if (node is JsonArray arr && arr.Count > 0 && arr[0] is not null)
        {
            foreach (var nested in Flatten(arr[0]!, $"{path}[]")) yield return nested;
        }
    }

    private static bool TryGetTargetNode(TransformationAgentRequestDto request, out JsonNode? target, out string? failure)
    {
        if (TryParseJson(request.TargetSamplePayload, out target, out failure)) return true;
        if (TryParseJson(request.TargetSchema, out var schema, out failure) && TryBuildSampleFromSchema(schema!, out target)) return true;
        target = null;
        return false;
    }

    private static bool TryBuildSampleFromSchema(JsonNode schema, out JsonNode? sample)
    {
        sample = null;
        var props = schema["properties"] as JsonObject ?? schema as JsonObject;
        if (props is null || props.Count == 0) return false;
        var obj = new JsonObject();
        foreach (var kvp in props) obj[kvp.Key] = "";
        sample = obj;
        return true;
    }

    private static bool TryParseJson(object? payload, out JsonNode? node, out string? failure)
    {
        node = null;
        failure = null;
        try
        {
            var json = payload switch
            {
                null => string.Empty,
                string s => s,
                JsonElement e => e.GetRawText(),
                JsonNode n => n.ToJsonString(),
                _ => JsonSerializer.Serialize(payload)
            };
            if (string.IsNullOrWhiteSpace(json)) { failure = "JSON is empty."; return false; }
            node = JsonNode.Parse(json);
            return node is not null;
        }
        catch (JsonException ex) { failure = ex.Message; return false; }
    }

    private static object? Truncate(object? value, int maxLength)
    {
        if (value is null) return null;
        var json = value switch { string s => s, JsonElement e => e.GetRawText(), JsonNode n => n.ToJsonString(), _ => JsonSerializer.Serialize(value) };
        return json.Length <= maxLength ? value : json[..Math.Max(0, maxLength)];
    }

    private static double CalculateConfidence(IReadOnlyCollection<WebhookFieldMappingRecommendationDto> mappings, IReadOnlyCollection<string> missingTargets, IReadOnlyCollection<string> unmappedSources)
    {
        var average = mappings.Count == 0 ? 0 : mappings.Average(mapping => mapping.ConfidenceScore);
        var penalty = missingTargets.Count * 0.15 + unmappedSources.Count(ContainsImportantFieldName) * 0.1;
        return Clamp(Math.Round(average - penalty, 2));
    }

    private static bool ContainsImportantFieldName(string path)
        => new[] { "id", "amount", "total", "price", "status", "state", "email", "customer", "order" }.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeName(string value) => NonAlphaNumericRegex().Replace(value, string.Empty).ToLowerInvariant();
    private static string NormalizeRisk(string? risk) => new[] { "Unknown", "Low", "Medium", "High", "Critical" }.FirstOrDefault(value => string.Equals(value, risk, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static string RemoveSecretLiterals(string code) => string.IsNullOrWhiteSpace(code) ? string.Empty : SecretLiteralRegex().Replace(code, "$1=\"***\"");
    private static void AddReason(TransformationAgentResponseDto response, TransformationAgentReasonCode code) { if (!response.ReasonCodes.Contains(code)) response.ReasonCodes.Add(code); }

    private sealed record FieldPath(string Name, string Path);

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("(?i)(secret|token|password|apikey)\\s*=\\s*\"[^\"]*\"")]
    private static partial Regex SecretLiteralRegex();
}
