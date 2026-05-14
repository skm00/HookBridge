using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;

public sealed partial class FluentValidationRuleGenerationAgent : IFluentValidationRuleGenerationAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly AiOptions _options;
    private readonly IFluentValidationPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly ILogger<FluentValidationRuleGenerationAgent> _logger;
    private static readonly string[] SensitiveTerms =
    [
        "Authorization", "Cookie", "Set-Cookie", "Token", "Secret", "Password", "Api-Key", "X-API-Key", "ClientSecret", "AccessToken", "ConnectionString"
    ];

    public FluentValidationRuleGenerationAgent(
        IOptions<AiOptions> options,
        IFluentValidationPromptBuilder promptBuilder,
        ILocalLlmClient llmClient,
        ILogger<FluentValidationRuleGenerationAgent> logger)
    {
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<FluentValidationRuleGenerationResponseDto> GenerateAsync(
        FluentValidationRuleGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (!_options.Enabled)
        {
            return CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic FluentValidation rules were generated.");
        }

        if (string.IsNullOrWhiteSpace(request.GeneratedDtoCode))
        {
            return CreateFallback(request, AiFallbackReason.InvalidResponse, "GeneratedDtoCode is missing; deterministic FluentValidation rules were generated.");
        }

        if (!TryParsePayload(request.Payload, out var payloadDocument, out _))
        {
            return CreateFallback(request, AiFallbackReason.InvalidJson, "Payload is not valid JSON.");
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
                    "FluentValidation rule generation AI response was invalid. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
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
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "FluentValidation rule generation fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, ex.Message);
        }
    }

    private bool TryParseResponse(string responseText, FluentValidationRuleGenerationRequestDto request, out FluentValidationRuleGenerationResponseDto response, out string failure)
    {
        response = new FluentValidationRuleGenerationResponseDto();
        failure = string.Empty;

        try
        {
            response = JsonSerializer.Deserialize<FluentValidationRuleGenerationResponseDto>(responseText, JsonOptions) ?? new FluentValidationRuleGenerationResponseDto();
        }
        catch (JsonException ex)
        {
            failure = $"invalid JSON: {ex.Message}";
            return false;
        }

        response.EventId = ValueOrDefault(response.EventId, request.EventId);
        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? request.CorrelationId : response.CorrelationId;
        response.ValidatorClassName = ValueOrDefault(response.ValidatorClassName, GetValidatorClassName(request.RootClassName));
        response.Namespace = string.IsNullOrWhiteSpace(response.Namespace) ? request.Namespace : response.Namespace;
        response.GeneratedValidatorCode = MaskSensitiveCode(response.GeneratedValidatorCode ?? string.Empty);
        response.Rules = response.Rules ?? Array.Empty<SuggestedValidationRuleDto>();
        response.Summary = ValueOrDefault(response.Summary, "FluentValidation rule generation completed by AI.");
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

    private FluentValidationRuleGenerationResponseDto CreateFallback(FluentValidationRuleGenerationRequestDto request, AiFallbackReason reason, string? message)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(message)) notes.Add(message);
        if (string.IsNullOrWhiteSpace(request.GeneratedDtoCode)) notes.Add("Generated DTO code was not supplied; fallback used payload and required fields only.");

        var rules = new List<SuggestedValidationRuleDto>();
        if (TryParsePayload(request.Payload, out var document, out var issue))
        {
            using (document)
            {
                var root = document.RootElement.ValueKind == JsonValueKind.Array
                    ? document.RootElement.EnumerateArray().FirstOrDefault(item => item.ValueKind == JsonValueKind.Object)
                    : document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    AddRulesForObject(rules, root, request.RequiredFields);
                }
                else
                {
                    notes.Add($"Root JSON value kind {document.RootElement.ValueKind} is not an object; generated validator shell only.");
                }
            }
        }
        else
        {
            notes.Add(issue);
        }

        foreach (var required in request.RequiredFields)
        {
            var propertyName = ToPropertyName(required);
            if (string.IsNullOrWhiteSpace(propertyName) || rules.Any(rule => rule.PropertyName == propertyName && rule.RuleType == "NotEmpty")) continue;
            rules.Add(CreateRule(propertyName, "NotEmpty", ".NotEmpty()", $"{propertyName} is required.", SuggestedValidationSeverity.Error, "Required field supplied by schema detection."));
        }

        var code = GenerateValidatorCode(request.RootClassName, request.Namespace, rules);
        var response = new FluentValidationRuleGenerationResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            ValidatorClassName = GetValidatorClassName(request.RootClassName),
            Namespace = request.Namespace,
            GeneratedValidatorCode = code,
            Rules = rules,
            Summary = rules.Count == 0 ? "Rule-based fallback generated a validator shell." : $"Rule-based fallback generated {rules.Count} FluentValidation rule(s).",
            ValidationNotes = notes.Where(note => !string.IsNullOrWhiteSpace(note)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ConfidenceScore = Clamp(rules.Count == 0 ? 0.2d : 0.45d),
            RiskLevel = rules.Count == 0 ? "Medium" : "Low",
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

    private static void AddRulesForObject(List<SuggestedValidationRuleDto> rules, JsonElement element, IReadOnlyList<string> requiredFields, string? parentCollection = null)
    {
        foreach (var property in element.EnumerateObject())
        {
            AddRulesForProperty(rules, property, requiredFields, parentCollection);
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                var firstObject = property.Value.EnumerateArray().FirstOrDefault(item => item.ValueKind == JsonValueKind.Object);
                if (firstObject.ValueKind == JsonValueKind.Object)
                {
                    AddRulesForObject(rules, firstObject, requiredFields, ToPropertyName(property.Name));
                }
            }
        }
    }

    private static void AddRulesForProperty(List<SuggestedValidationRuleDto> rules, JsonProperty property, IReadOnlyList<string> requiredFields, string? parentCollection = null)
    {
        var leafPropertyName = ToPropertyName(property.Name);
        var propertyName = string.IsNullOrWhiteSpace(parentCollection) ? leafPropertyName : $"{parentCollection}.{leafPropertyName}";
        var lower = property.Name.ToLowerInvariant();
        var isRequired = requiredFields.Any(field => MatchesField(field, property.Name, leafPropertyName));

        if ((isRequired || lower.EndsWith("id", StringComparison.Ordinal) || lower == "id") && property.Value.ValueKind == JsonValueKind.String)
        {
            rules.Add(CreateRule(propertyName, "NotEmpty", ".NotEmpty()", $"{propertyName} is required.", SuggestedValidationSeverity.Error, "Required or identifier string should not be empty."));
        }

        if (property.Value.ValueKind == JsonValueKind.Array)
        {
            rules.Add(CreateRule(propertyName, "NotNull", ".NotNull()", $"{propertyName} cannot be null.", SuggestedValidationSeverity.Error, "Array fields should be initialized."));
            if (isRequired)
            {
                rules.Add(CreateRule(propertyName, "NotEmpty", ".NotEmpty()", $"{propertyName} must contain at least one item.", SuggestedValidationSeverity.Error, "Required array fields should contain items."));
            }
        }

        if (lower.Contains("email", StringComparison.Ordinal))
        {
            rules.Add(CreateRule(propertyName, "EmailAddress", ".EmailAddress()", $"{propertyName} must be a valid email address.", SuggestedValidationSeverity.Warning, "Email-like field inferred from property name."));
        }

        if (lower.Contains("url", StringComparison.Ordinal) || lower.Contains("uri", StringComparison.Ordinal))
        {
            rules.Add(CreateRule(propertyName, "Url", ".Must(value => Uri.TryCreate(value, UriKind.Absolute, out _))", $"{propertyName} must be a valid absolute URL.", SuggestedValidationSeverity.Warning, "URL-like field inferred from property name."));
        }

        if (lower.Contains("quantity", StringComparison.Ordinal) || lower.Contains("count", StringComparison.Ordinal) || lower.Contains("amount", StringComparison.Ordinal) || lower.Contains("price", StringComparison.Ordinal) || lower.Contains("total", StringComparison.Ordinal))
        {
            rules.Add(CreateRule(propertyName, "GreaterThanOrEqualTo", ".GreaterThanOrEqualTo(0)", $"{propertyName} must be greater than or equal to 0.", SuggestedValidationSeverity.Warning, "Non-negative numeric field inferred from property name."));
        }

        if ((lower.Contains("date", StringComparison.Ordinal) || lower.Contains("time", StringComparison.Ordinal) || lower.EndsWith("atutc", StringComparison.Ordinal)) && property.Value.ValueKind == JsonValueKind.String)
        {
            rules.Add(CreateRule(propertyName, "UtcDateTime", ".Must(value => value.Kind == DateTimeKind.Utc)", $"{propertyName} must be a UTC date/time value.", SuggestedValidationSeverity.Warning, "Date/time-like field should be represented as UTC when possible."));
        }
    }

    private static string GenerateValidatorCode(string rootClassName, string? dtoNamespace, IReadOnlyList<SuggestedValidationRuleDto> rules)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using FluentValidation;");
        if (rules.Any(rule => rule.RuleType is "Url" or "UtcDateTime"))
        {
            builder.AppendLine("using System;");
        }
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(dtoNamespace))
        {
            builder.Append("namespace ").Append(dtoNamespace).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("public sealed class ").Append(GetValidatorClassName(rootClassName)).Append(" : AbstractValidator<").Append(rootClassName).AppendLine(">");
        builder.AppendLine("{");
        builder.Append("    public ").Append(GetValidatorClassName(rootClassName)).AppendLine("()");
        builder.AppendLine("    {");

        foreach (var group in rules.GroupBy(rule => new { rule.PropertyName, rule.RuleType, rule.RuleExpression, rule.ErrorMessage }))
        {
            var rule = group.First();
            if (rule.PropertyName.Contains('.', StringComparison.Ordinal))
            {
                var parts = rule.PropertyName.Split('.', 2);
                builder.Append("        RuleForEach(x => x.").Append(parts[0]).AppendLine(")");
                builder.AppendLine("            .ChildRules(item =>");
                builder.AppendLine("            {");
                builder.Append("                item.RuleFor(x => x.").Append(parts[1]).AppendLine(")");
                builder.Append("                    ").Append(rule.RuleExpression).AppendLine();
                builder.Append("                    .WithMessage(\"").Append(Escape(rule.ErrorMessage)).AppendLine("\");");
                builder.AppendLine("            });");
                builder.AppendLine();
                continue;
            }

            builder.Append("        RuleFor(x => x.").Append(rule.PropertyName).AppendLine(")");
            builder.Append("            ").Append(rule.RuleExpression).AppendLine();
            if (rule.RuleType is "EmailAddress" or "Url")
            {
                builder.Append("            .When(x => !string.IsNullOrWhiteSpace(x.").Append(rule.PropertyName).AppendLine("))");
            }
            builder.Append("            .WithMessage(\"").Append(Escape(rule.ErrorMessage)).AppendLine("\");");
            builder.AppendLine();
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
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

    private static SuggestedValidationRuleDto CreateRule(string propertyName, string ruleType, string ruleExpression, string errorMessage, SuggestedValidationSeverity severity, string description)
        => new() { PropertyName = propertyName, RuleType = ruleType, RuleExpression = ruleExpression, ErrorMessage = errorMessage, Severity = severity, Description = description };

    private static string GetValidatorClassName(string rootClassName) => $"{rootClassName}Validator";
    private static string ToPropertyName(string value) => string.Concat(NonAlphaNumericRegex().Split(value.Split('.').Last()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(ToPascalCase));
    private static string ToPascalCase(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + (value.Length == 1 ? string.Empty : value[1..]);
    private static bool MatchesField(string field, string jsonName, string propertyName) => string.Equals(field, jsonName, StringComparison.OrdinalIgnoreCase) || string.Equals(ToPropertyName(field), propertyName, StringComparison.OrdinalIgnoreCase) || field.EndsWith($".{jsonName}", StringComparison.OrdinalIgnoreCase);
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    private static string MaskSensitiveCode(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = SensitiveLiteralRegex(term).Replace(masked, match => $"{match.Groups["prefix"].Value}***MASKED***{match.Groups["suffix"].Value}");
        }

        return masked;
    }
    private static string ValueOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static DateTime EnsureUtc(DateTime value) => value.Kind switch { DateTimeKind.Utc => value, DateTimeKind.Local => value.ToUniversalTime(), _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) };
    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Min(1, Math.Max(0, value));
    private static string NormalizeRiskLevel(string? riskLevel)
    {
        var allowed = new[] { "Unknown", "Low", "Medium", "High", "Critical" };
        return allowed.FirstOrDefault(level => string.Equals(level, riskLevel, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    }

    private static void ValidateRequest(FluentValidationRuleGenerationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) throw new ArgumentException("EventId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RootClassName)) throw new ArgumentException("RootClassName is required.", nameof(request));
        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
        if (!IsValidClassName(request.RootClassName)) throw new ArgumentException("RootClassName must be a valid C# class name.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.Namespace) && !IsValidNamespace(request.Namespace)) throw new ArgumentException("Namespace must be a valid C# namespace.", nameof(request));
    }

    private static void ValidateResponse(FluentValidationRuleGenerationResponseDto response)
    {
        if (response.ConfidenceScore is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(response), "ConfidenceScore must be between 0 and 1.");
        if (response.GeneratedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("GeneratedAtUtc must be UTC.", nameof(response));
    }

    private static bool IsValidClassName(string value) => CSharpIdentifierRegex().IsMatch(value) && !CSharpKeywords.Contains(value);
    private static bool IsValidNamespace(string value) => value.Split('.').All(IsValidClassName);

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "class", "namespace", "public", "private", "protected", "internal", "void", "string", "int", "long", "decimal", "bool", "object", "event", "base", "new", "null", "true", "false"
    };

    [GeneratedRegex("[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();

    private static Regex SensitiveLiteralRegex(string term) => new(
        $"(?<prefix>{Regex.Escape(term)}[^\\r\\n\"]*\")[^\"]*(?<suffix>\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpIdentifierRegex();
}
