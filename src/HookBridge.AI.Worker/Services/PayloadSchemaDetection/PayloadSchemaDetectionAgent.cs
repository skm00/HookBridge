using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.PayloadSchemaDetection;

public sealed partial class PayloadSchemaDetectionAgent : IPayloadSchemaDetectionAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiOptions _options;
    private readonly IPayloadSchemaDetectionPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<PayloadSchemaDetectionAgent> _logger;

    public PayloadSchemaDetectionAgent(
        IOptions<AiOptions> options,
        IPayloadSchemaDetectionPromptBuilder promptBuilder,
        ILocalLlmClient llmClient,
        ILogger<PayloadSchemaDetectionAgent> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<PayloadSchemaDetectionResponseDto> DetectAsync(
        PayloadSchemaDetectionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (!_options.Enabled)
        {
            return await Task.FromResult(CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; rule-based schema detection was used."));
        }

        if (!TryParsePayload(request.Payload, out var payloadDocument, out _))
        {
            return await Task.FromResult(CreateFallback(request, AiFallbackReason.InvalidJson, "Payload is not valid JSON."));
        }

        payloadDocument.Dispose();

        try
        {
            var prompt = _promptBuilder.BuildPrompt(request);
            var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);
            if (!llmResponse.IsSuccess)
            {
                return CreateFallback(request, llmResponse.FallbackReason, llmResponse.ErrorMessage);
            }

            if (!TryParseResponse(llmResponse.ResponseText, request, out var response, out var failure))
            {
                _logger.LogWarning(
                    "Payload schema detection AI response was invalid. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                    request.EventId,
                    request.CorrelationId,
                    failure);
                return CreateFallback(request, AiFallbackReason.InvalidJson, $"AI response could not be used: {failure}");
            }

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
                "Payload schema detection fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, exception.Message);
        }
    }

    private bool TryParseResponse(
        string responseText,
        PayloadSchemaDetectionRequestDto request,
        out PayloadSchemaDetectionResponseDto response,
        out string failure)
    {
        response = new PayloadSchemaDetectionResponseDto();
        failure = string.Empty;

        try
        {
            response = JsonSerializer.Deserialize<PayloadSchemaDetectionResponseDto>(responseText, JsonOptions) ?? new PayloadSchemaDetectionResponseDto();
        }
        catch (JsonException ex)
        {
            failure = $"invalid JSON: {ex.Message}";
            return false;
        }

        response.EventId = string.IsNullOrWhiteSpace(response.EventId) ? request.EventId : response.EventId;
        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? request.CorrelationId : response.CorrelationId;
        response.DetectedSchemaName = ValueOrDefault(response.DetectedSchemaName, ValueOrDefault(request.EventType, "UnknownPayload"));
        response.DetectedEventType = ValueOrDefault(response.DetectedEventType, ValueOrDefault(request.EventType, "Unknown"));
        response.Summary = ValueOrDefault(response.Summary, "Payload schema detection completed by AI.");
        response.ImportantFields = response.ImportantFields ?? Array.Empty<PayloadFieldInsightDto>();
        response.MissingFields = response.MissingFields ?? Array.Empty<string>();
        response.ValidationIssues = response.ValidationIssues ?? Array.Empty<string>();
        response.SuggestedDtoName = ValueOrDefault(response.SuggestedDtoName, GenerateSuggestedDtoName(request.EventType));
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

    private PayloadSchemaDetectionResponseDto CreateFallback(
        PayloadSchemaDetectionRequestDto request,
        AiFallbackReason reason,
        string? message)
    {
        var issues = new List<string>();
        if (!string.IsNullOrWhiteSpace(message))
        {
            issues.Add(message);
        }

        var importantFields = new List<PayloadFieldInsightDto>();
        var detectedSchemaName = ValueOrDefault(request.EventType, "UnknownPayload");
        var detectedEventType = ValueOrDefault(request.EventType, "Unknown");
        var riskLevel = "Unknown";
        var summary = "Rule-based fallback could not inspect the payload because it was not valid JSON.";
        var confidence = 0.2d;

        if (TryParsePayload(request.Payload, out var document, out var parseIssue))
        {
            using (document)
            {
                var root = document.RootElement;
                InferFields(root, "$", importantFields, 0, 50);
                var rootKind = root.ValueKind == JsonValueKind.Array ? "array" : root.ValueKind == JsonValueKind.Object ? "object" : root.ValueKind.ToString().ToLowerInvariant();
                summary = $"Rule-based fallback detected a JSON root {rootKind} with {importantFields.Count} visible field(s).";
                riskLevel = importantFields.Count == 0 ? "Medium" : "Unknown";
                confidence = root.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? 0.45d : 0.35d;
            }
        }
        else
        {
            issues.Add(parseIssue);
            riskLevel = "Medium";
        }

        var response = new PayloadSchemaDetectionResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            DetectedSchemaName = detectedSchemaName,
            DetectedEventType = detectedEventType,
            Summary = summary,
            ImportantFields = importantFields,
            MissingFields = Array.Empty<string>(),
            ValidationIssues = issues.Where(issue => !string.IsNullOrWhiteSpace(issue)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            SuggestedDtoName = GenerateSuggestedDtoName(request.EventType),
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

    private static void InferFields(JsonElement element, string path, ICollection<PayloadFieldInsightDto> fields, int depth, int maxFields)
    {
        if (depth > 6 || fields.Count >= maxFields)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (fields.Count >= maxFields)
                {
                    return;
                }

                var propertyPath = path == "$" ? $"$.{property.Name}" : $"{path}.{property.Name}";
                fields.Add(new PayloadFieldInsightDto
                {
                    FieldName = property.Name,
                    JsonPath = propertyPath,
                    InferredType = InferType(property.Value),
                    IsRequired = true,
                    SampleValue = SampleValue(property.Value),
                    Description = $"Observed {InferType(property.Value)} field."
                });

                InferFields(property.Value, propertyPath, fields, depth + 1, maxFields);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var first = element.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                InferFields(first, $"{path}[0]", fields, depth + 1, maxFields);
            }
        }
    }

    private static string InferType(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String when DateTime.TryParse(value.GetString(), out _) => "datetime",
            JsonValueKind.String => "string",
            JsonValueKind.Number when value.TryGetInt64(out _) => "integer",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.Null => "null",
            _ => "unknown"
        };

    private static string? SampleValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null => null,
            JsonValueKind.Object => "{...}",
            JsonValueKind.Array => "[...]",
            _ => null
        };

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

    public static string GenerateSuggestedDtoName(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return "PayloadSchemaDto";
        }

        var parts = NonAlphaNumericRegex().Split(eventType)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(ToPascalCase);
        var name = string.Concat(parts);
        return string.IsNullOrWhiteSpace(name) ? "PayloadSchemaDto" : $"{name}Dto";
    }

    private static string ToPascalCase(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + (value.Length == 1 ? string.Empty : value[1..]);

    private static void ValidateRequest(PayloadSchemaDetectionRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            throw new ArgumentException("EventId is required.", nameof(request));
        }

        if (request.Payload is null || request.Payload is string text && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Payload is required.", nameof(request));
        }

        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
        }
    }

    private static void ValidateResponse(PayloadSchemaDetectionResponseDto response)
    {
        if (response.ConfidenceScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(response), "ConfidenceScore must be between 0 and 1.");
        }

        if (response.GeneratedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("GeneratedAtUtc must be UTC.", nameof(response));
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static double Clamp(double value)
        => double.IsNaN(value) ? 0 : Math.Min(1, Math.Max(0, value));

    private static string NormalizeRiskLevel(string? riskLevel)
    {
        var allowed = new[] { "Unknown", "Low", "Medium", "High", "Critical" };
        return allowed.FirstOrDefault(level => string.Equals(level, riskLevel, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    }

    private static string ValueOrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();
}
