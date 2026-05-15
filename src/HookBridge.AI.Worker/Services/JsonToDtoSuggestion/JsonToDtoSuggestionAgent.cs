using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.PromptVersioning;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.JsonToDtoSuggestion;

public sealed partial class JsonToDtoSuggestionAgent : IJsonToDtoSuggestionAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly AiOptions _options;
    private readonly IJsonToDtoPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<JsonToDtoSuggestionAgent> _logger;

    public JsonToDtoSuggestionAgent(
        IOptions<AiOptions> options,
        IJsonToDtoPromptBuilder promptBuilder,
        ILocalLlmClient llmClient,
        ILogger<JsonToDtoSuggestionAgent> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<JsonToDtoSuggestionResponseDto> SuggestAsync(
        JsonToDtoSuggestionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (!_options.Enabled)
        {
            return await Task.FromResult(CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic DTO generation was used."));
        }

        if (!TryParsePayload(request.Payload, out var payloadDocument, out _))
        {
            return await Task.FromResult(CreateFallback(request, AiFallbackReason.InvalidJson, "Payload is not valid JSON."));
        }

        payloadDocument.Dispose();

        try
        {
            var promptResult = await _promptBuilder.BuildPromptWithMetadataAsync(request, cancellationToken);
            var prompt = promptResult.Content;
            var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);
            if (!llmResponse.IsSuccess)
            {
                return CreateFallback(request, llmResponse.FallbackReason, llmResponse.ErrorMessage);
            }

            if (!TryParseResponse(llmResponse.ResponseText, request, out var response, out var failure))
            {
                _logger.LogWarning(
                    "JSON-to-DTO suggestion AI response was invalid. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                    request.EventId,
                    request.CorrelationId,
                    failure);
                return CreateFallback(request, AiFallbackReason.InvalidJson, $"AI response could not be used: {failure}");
            }

            response.ApplyPromptMetadata(promptResult.Metadata);
            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "JSON-to-DTO suggestion fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, exception.Message);
        }
    }

    private bool TryParseResponse(
        string responseText,
        JsonToDtoSuggestionRequestDto request,
        out JsonToDtoSuggestionResponseDto response,
        out string failure)
    {
        response = new JsonToDtoSuggestionResponseDto();
        failure = string.Empty;

        try
        {
            response = JsonSerializer.Deserialize<JsonToDtoSuggestionResponseDto>(responseText, JsonOptions) ?? new JsonToDtoSuggestionResponseDto();
        }
        catch (JsonException ex)
        {
            failure = $"invalid JSON: {ex.Message}";
            return false;
        }

        response.EventId = ValueOrDefault(response.EventId, request.EventId);
        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? request.CorrelationId : response.CorrelationId;
        response.SuggestedRootClassName = ValueOrDefault(response.SuggestedRootClassName, ResolveRootClassName(request));
        response.Namespace = string.IsNullOrWhiteSpace(response.Namespace) ? request.Namespace : response.Namespace;
        response.GeneratedCode = ValueOrDefault(response.GeneratedCode, string.Empty);
        response.Classes = response.Classes ?? Array.Empty<SuggestedDtoClassDto>();
        response.Summary = ValueOrDefault(response.Summary, "JSON-to-DTO suggestion completed by AI.");
        response.ValidationNotes = response.ValidationNotes ?? Array.Empty<string>();
        response.ConfidenceScore = Clamp(response.ConfidenceScore);
        response.RiskLevel = NormalizeRiskLevel(response.RiskLevel);
        response.GeneratedAtUtc = EnsureUtc(response.GeneratedAtUtc == default ? DateTime.UtcNow : response.GeneratedAtUtc);
        response.Model = _options.Model;
        response.Provider = _options.Provider;
        response.Fallback = new AiFallbackMetadataDto
        {
            UsedFallback = false,
            FallbackReason = AiFallbackReason.None,
            Provider = _options.Provider,
            Model = _options.Model,
            GeneratedAtUtc = DateTime.UtcNow
        };

        ValidateResponse(response);
        return true;
    }

    private JsonToDtoSuggestionResponseDto CreateFallback(JsonToDtoSuggestionRequestDto request, AiFallbackReason reason, string? message)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(message)) notes.Add(message);

        var rootClassName = ResolveRootClassName(request);
        var dtoNamespace = request.Namespace;
        var classes = new List<SuggestedDtoClassDto>();
        string generatedCode;
        var confidence = 0.25d;
        var riskLevel = "Medium";
        var summary = "Rule-based fallback could not generate DTOs because the payload was not valid JSON.";

        if (TryParsePayload(request.Payload, out var document, out var parseIssue))
        {
            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    BuildClass(rootClassName, root, classes, rootClassName);
                    generatedCode = GenerateCode(classes, dtoNamespace);
                    summary = $"Rule-based fallback generated {classes.Count} DTO class(es) from the visible JSON object.";
                    confidence = 0.55d;
                    riskLevel = "Low";
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    var element = root.EnumerateArray().FirstOrDefault();
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        BuildClass(rootClassName, element, classes, rootClassName);
                        generatedCode = GenerateCode(classes, dtoNamespace);
                        summary = $"Rule-based fallback generated {classes.Count} DTO class(es) from the first visible array item.";
                        confidence = 0.45d;
                        riskLevel = "Medium";
                        notes.Add("Root payload is an array; generated root class from the first item only.");
                    }
                    else
                    {
                        generatedCode = GenerateCode([new SuggestedDtoClassDto { ClassName = rootClassName, Description = "Root payload wrapper.", Properties = [] }], dtoNamespace);
                        notes.Add("Root array item type could not be represented as object properties.");
                    }
                }
                else
                {
                    generatedCode = GenerateCode([new SuggestedDtoClassDto { ClassName = rootClassName, Description = "Root payload wrapper.", Properties = [] }], dtoNamespace);
                    notes.Add($"Root JSON value kind {root.ValueKind} is not an object.");
                }
            }
        }
        else
        {
            generatedCode = GenerateCode([new SuggestedDtoClassDto { ClassName = rootClassName, Description = "Fallback DTO shell for invalid JSON payload.", Properties = [] }], dtoNamespace);
            notes.Add(parseIssue);
        }

        var response = new JsonToDtoSuggestionResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            SuggestedRootClassName = rootClassName,
            Namespace = dtoNamespace,
            GeneratedCode = generatedCode,
            Classes = classes.Count == 0 ? [new SuggestedDtoClassDto { ClassName = rootClassName, Properties = [], Description = "Fallback DTO shell." }] : classes,
            Summary = summary,
            ValidationNotes = notes.Where(note => !string.IsNullOrWhiteSpace(note)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ConfidenceScore = Clamp(confidence),
            RiskLevel = riskLevel,
            GeneratedAtUtc = DateTime.UtcNow,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = new AiFallbackMetadataDto
            {
                UsedFallback = true,
                FallbackReason = reason,
                FallbackMessage = message ?? string.Empty,
                Provider = _options.Provider,
                Model = _options.Model,
                GeneratedAtUtc = DateTime.UtcNow
            }
        };

        ValidateResponse(response);
        return response;
    }

    private static void BuildClass(string className, JsonElement element, ICollection<SuggestedDtoClassDto> classes, string rootClassName)
    {
        if (classes.Any(c => c.ClassName == className)) return;

        var properties = new List<SuggestedDtoPropertyDto>();
        foreach (var property in element.EnumerateObject())
        {
            var type = InferCSharpType(property.Name, property.Value, classes, rootClassName, className, out var nullable);
            properties.Add(new SuggestedDtoPropertyDto
            {
                PropertyName = ToIdentifier(property.Name, pascalCase: true),
                JsonName = property.Name,
                CSharpType = type,
                IsNullable = nullable || property.Value.ValueKind == JsonValueKind.Null,
                IsRequired = property.Value.ValueKind != JsonValueKind.Null,
                Description = $"Mapped from JSON property '{property.Name}'."
            });
        }

        classes.Add(new SuggestedDtoClassDto
        {
            ClassName = className,
            Properties = properties,
            Description = $"DTO generated for {className}."
        });
    }

    private static string InferCSharpType(string jsonName, JsonElement value, ICollection<SuggestedDtoClassDto> classes, string rootClassName, string parentClassName, out bool nullable)
    {
        nullable = false;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                nullable = true;
                return DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _) ? "DateTime" : "string";
            case JsonValueKind.Number:
                if (value.TryGetInt32(out _)) return "int";
                if (value.TryGetInt64(out _)) return "long";
                return "decimal";
            case JsonValueKind.True:
            case JsonValueKind.False:
                return "bool";
            case JsonValueKind.Object:
                nullable = true;
                var objectClassName = ToNestedClassName(jsonName, rootClassName, parentClassName, false);
                BuildClass(objectClassName, value, classes, rootClassName);
                return objectClassName;
            case JsonValueKind.Array:
                nullable = true;
                return $"List<{InferArrayType(jsonName, value, classes, rootClassName, parentClassName)}>";
            case JsonValueKind.Null:
                nullable = true;
                return "object";
            default:
                nullable = true;
                return "JsonElement";
        }
    }

    private static string InferArrayType(string jsonName, JsonElement value, ICollection<SuggestedDtoClassDto> classes, string rootClassName, string parentClassName)
    {
        var first = value.EnumerateArray().FirstOrDefault(item => item.ValueKind != JsonValueKind.Null && item.ValueKind != JsonValueKind.Undefined);
        if (first.ValueKind == JsonValueKind.Undefined) return "object";
        if (first.ValueKind == JsonValueKind.Object)
        {
            var itemClassName = ToNestedClassName(jsonName, rootClassName, parentClassName, true);
            BuildClass(itemClassName, first, classes, rootClassName);
            return itemClassName;
        }

        var itemType = InferCSharpType(jsonName, first, classes, rootClassName, parentClassName, out _);
        return itemType == "JsonElement" ? "JsonElement" : itemType;
    }

    private static string GenerateCode(IReadOnlyCollection<SuggestedDtoClassDto> classes, string? dtoNamespace)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Text.Json.Serialization;");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(dtoNamespace))
        {
            builder.Append("namespace ").Append(dtoNamespace).AppendLine(";");
            builder.AppendLine();
        }

        foreach (var dtoClass in classes)
        {
            builder.Append("public sealed class ").AppendLine(dtoClass.ClassName);
            builder.AppendLine("{");
            foreach (var property in dtoClass.Properties)
            {
                builder.Append("    [JsonPropertyName(\"").Append(property.JsonName).AppendLine("\")]");
                var suffix = property.IsNullable && !property.CSharpType.EndsWith('>') && (property.CSharpType == "string" || property.CSharpType == "object" || property.CSharpType.EndsWith("Dto", StringComparison.Ordinal)) ? "?" : string.Empty;
                var listSuffix = property.IsNullable && property.CSharpType.StartsWith("List<", StringComparison.Ordinal) ? "?" : string.Empty;
                builder.Append("    public ").Append(property.CSharpType).Append(suffix).Append(listSuffix).Append(' ').Append(property.PropertyName).AppendLine(" { get; set; }");
                builder.AppendLine();
            }
            builder.AppendLine("}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static bool TryParsePayload(object? payload, out JsonDocument document, out string issue)
    {
        document = null!;
        issue = string.Empty;
        try
        {
            var json = payload switch
            {
                null => string.Empty,
                string text => text,
                JsonElement element => element.GetRawText(),
                _ => JsonSerializer.Serialize(payload)
            };
            if (string.IsNullOrWhiteSpace(json))
            {
                issue = "Payload is required.";
                return false;
            }
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException ex)
        {
            issue = $"Payload is invalid JSON: {ex.Message}";
            return false;
        }
    }

    public static string GenerateRootClassName(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "WebhookPayloadDto";
        var name = string.Concat(NonAlphaNumericRegex().Split(eventType).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => ToIdentifier(p, true)));
        return string.IsNullOrWhiteSpace(name) ? "WebhookPayloadDto" : name.EndsWith("Dto", StringComparison.Ordinal) ? name : $"{name}Dto";
    }

    private static string ResolveRootClassName(JsonToDtoSuggestionRequestDto request)
        => string.IsNullOrWhiteSpace(request.RootClassName) ? GenerateRootClassName(request.EventType) : request.RootClassName!;

    private static string ToNestedClassName(string jsonName, string rootClassName, string parentClassName, bool arrayItem)
    {
        var baseName = ToIdentifier(Singularize(jsonName), true);
        if (arrayItem && string.Equals(jsonName, "items", StringComparison.OrdinalIgnoreCase))
        {
            var rootBase = rootClassName.EndsWith("Dto", StringComparison.Ordinal) ? rootClassName[..^3] : rootClassName;
            return $"{rootBase}ItemDto";
        }
        return baseName.EndsWith("Dto", StringComparison.Ordinal) ? baseName : $"{baseName}Dto";
    }

    private static string Singularize(string name)
        => name.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && name.Length > 3 ? $"{name[..^3]}y" :
           name.EndsWith('s') && name.Length > 1 ? name[..^1] : name;

    private static string ToIdentifier(string value, bool pascalCase)
    {
        var parts = NonAlphaNumericRegex().Split(value).Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        var name = string.Concat(parts.Select(ToPascalCase));
        if (string.IsNullOrWhiteSpace(name)) name = pascalCase ? "Value" : "value";
        if (char.IsDigit(name[0])) name = $"_{name}";
        return CSharpKeywords.Contains(name) ? $"{name}_" : name;
    }

    private static string ToPascalCase(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + (value.Length == 1 ? string.Empty : value[1..]);

    private static void ValidateRequest(JsonToDtoSuggestionRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) throw new ArgumentException("EventId is required.", nameof(request));
        if (request.Payload is null || request.Payload is string text && string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Payload is required.", nameof(request));
        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.RootClassName) && !IsValidClassName(request.RootClassName)) throw new ArgumentException("RootClassName must be a valid C# class name.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.Namespace) && !IsValidNamespace(request.Namespace)) throw new ArgumentException("Namespace must be a valid C# namespace.", nameof(request));
    }

    private static void ValidateResponse(JsonToDtoSuggestionResponseDto response)
    {
        if (response.ConfidenceScore is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(response), "ConfidenceScore must be between 0 and 1.");
        if (response.GeneratedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("GeneratedAtUtc must be UTC.", nameof(response));
    }

    private static bool IsValidClassName(string value)
        => CSharpIdentifierRegex().IsMatch(value) && !CSharpKeywords.Contains(value);

    private static bool IsValidNamespace(string value)
        => value.Split('.').All(IsValidClassName);

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Min(1, Math.Max(0, value));

    private static string NormalizeRiskLevel(string? riskLevel)
    {
        var allowed = new[] { "Unknown", "Low", "Medium", "High", "Critical" };
        return allowed.FirstOrDefault(level => string.Equals(level, riskLevel, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    }

    private static string ValueOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "class", "namespace", "public", "private", "protected", "internal", "void", "string", "int", "long", "decimal", "bool", "object", "event", "base", "new", "null", "true", "false"
    };

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpIdentifierRegex();
}
