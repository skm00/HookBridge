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
            return CreateFallback(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic FluentValidation rules were used.");

        if (string.IsNullOrWhiteSpace(request.GeneratedDtoCode))
            return CreateFallback(request, AiFallbackReason.ConfigurationError, "GeneratedDtoCode is missing; deterministic FluentValidation rules were used.");

        if (!TryParsePayload(request.Payload, out var document, out _))
            return CreateFallback(request, AiFallbackReason.InvalidJson, "Payload is not valid JSON.");
        document?.Dispose();

        try
        {
            var llmResponse = await _llmClient.GenerateAsync(_promptBuilder.BuildPrompt(request), cancellationToken);
            if (!llmResponse.IsSuccess)
                return CreateFallback(request, llmResponse.FallbackReason, llmResponse.ErrorMessage);

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
            _logger.LogWarning(ex, "FluentValidation rule generation fallback used after LLM failure. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            return CreateFallback(request, AiFallbackReason.UnknownError, ex.Message);
        }
    }

    private bool TryParseResponse(string responseText, FluentValidationRuleGenerationRequestDto request, out FluentValidationRuleGenerationResponseDto response, out string failure)
    {
        response = new FluentValidationRuleGenerationResponseDto();
        failure = string.Empty;
        try
        {
            response = JsonSerializer.Deserialize<FluentValidationRuleGenerationResponseDto>(responseText, JsonOptions) ?? new();
        }
        catch (JsonException ex)
        {
            failure = $"invalid JSON: {ex.Message}";
            return false;
        }

        response.EventId = ValueOrDefault(response.EventId, request.EventId);
        response.CorrelationId = string.IsNullOrWhiteSpace(response.CorrelationId) ? request.CorrelationId : response.CorrelationId;
        response.ValidatorClassName = ValueOrDefault(response.ValidatorClassName, GetValidatorName(request.RootClassName));
        response.Namespace = string.IsNullOrWhiteSpace(response.Namespace) ? request.Namespace : response.Namespace;
        response.GeneratedValidatorCode = RemoveSensitiveLines(ValueOrDefault(response.GeneratedValidatorCode, string.Empty));
        response.Rules ??= Array.Empty<SuggestedValidationRuleDto>();
        response.Summary = ValueOrDefault(response.Summary, "FluentValidation rule generation completed by AI.");
        response.ValidationNotes ??= Array.Empty<string>();
        response.ConfidenceScore = Clamp(response.ConfidenceScore);
        response.RiskLevel = NormalizeRisk(response.RiskLevel);
        response.GeneratedAtUtc = EnsureUtc(response.GeneratedAtUtc == default ? DateTime.UtcNow : response.GeneratedAtUtc);
        response.Model = _options.Model;
        response.Provider = _options.Provider;
        response.Fallback = new AiFallbackMetadataDto { UsedFallback = false, FallbackReason = AiFallbackReason.None, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = DateTime.UtcNow };
        ValidateResponse(response);
        return true;
    }

    private FluentValidationRuleGenerationResponseDto CreateFallback(FluentValidationRuleGenerationRequestDto request, AiFallbackReason reason, string? message)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(message)) notes.Add(message);
        var rules = new List<SuggestedValidationRuleDto>();
        JsonDocument? doc = null;
        if (TryParsePayload(request.Payload, out doc, out var parseFailure))
        {
            using (doc)
            {
                if (doc!.RootElement.ValueKind == JsonValueKind.Object)
                    InferRules(doc.RootElement, request.RequiredFields ?? Array.Empty<string>(), rules);
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    rules.Add(Rule("Items", "NotNull", ".NotNull()", "Items cannot be null.", "Root array payload should not be null."));
            }
        }
        else
        {
            notes.Add($"Payload invalid JSON; generated a shell validator only. {parseFailure}");
        }

        foreach (var field in request.RequiredFields ?? Array.Empty<string>())
        {
            var prop = ToPascal(field);
            if (!rules.Any(r => r.PropertyName == prop && r.RuleType == "NotEmpty"))
                rules.Insert(0, Rule(prop, "NotEmpty", ".NotEmpty()", $"{prop} is required.", "Required field should be populated.", SuggestedValidationSeverity.Error));
        }

        var code = GenerateValidatorCode(request.RootClassName, request.Namespace, rules);
        return new FluentValidationRuleGenerationResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            ValidatorClassName = GetValidatorName(request.RootClassName),
            Namespace = request.Namespace,
            GeneratedValidatorCode = code,
            Rules = rules,
            Summary = "Generated deterministic FluentValidation rules from visible payload fields.",
            ValidationNotes = notes,
            ConfidenceScore = doc is null ? 0.2 : 0.45,
            RiskLevel = doc is null ? "Medium" : "Low",
            GeneratedAtUtc = DateTime.UtcNow,
            Model = _options.Model,
            Provider = _options.Provider,
            Fallback = new AiFallbackMetadataDto { UsedFallback = true, FallbackReason = reason, FallbackMessage = message ?? string.Empty, Provider = _options.Provider, Model = _options.Model, GeneratedAtUtc = DateTime.UtcNow }
        };
    }

    private static void InferRules(JsonElement element, IReadOnlyList<string> requiredFields, List<SuggestedValidationRuleDto> rules, string prefix = "")
    {
        var required = new HashSet<string>(requiredFields.Select(ToPascal), StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            var prop = string.Concat(prefix, ToPascal(property.Name));
            var lower = property.Name.ToLowerInvariant();
            if ((lower.EndsWith("id", StringComparison.Ordinal) || required.Contains(prop)) && property.Value.ValueKind == JsonValueKind.String)
                rules.Add(Rule(prop, "NotEmpty", ".NotEmpty()", $"{prop} is required.", "Identifier or required string should be populated.", SuggestedValidationSeverity.Error));
            if (lower.Contains("email", StringComparison.Ordinal))
                rules.Add(Rule(prop, "EmailAddress", ".EmailAddress()", $"{prop} must be a valid email address.", "Email-like field should contain a valid email."));
            if (lower.Contains("url", StringComparison.Ordinal) || lower.Contains("uri", StringComparison.Ordinal))
                rules.Add(Rule(prop, "AbsoluteUri", ".Must(value => Uri.TryCreate(value, UriKind.Absolute, out _))", $"{prop} must be a valid absolute URL.", "URL-like field should be an absolute URI."));
            if (lower.Contains("quantity", StringComparison.Ordinal) || lower.Contains("count", StringComparison.Ordinal) || lower.Contains("amount", StringComparison.Ordinal) || lower.Contains("price", StringComparison.Ordinal) || lower.Contains("total", StringComparison.Ordinal))
                rules.Add(Rule(prop, "GreaterThanOrEqualTo", ".GreaterThanOrEqualTo(0)", $"{prop} must be greater than or equal to 0.", "Numeric amount, price, total, quantity, or count should not be negative."));
            if ((lower.Contains("date", StringComparison.Ordinal) || lower.EndsWith("at", StringComparison.Ordinal) || lower.Contains("time", StringComparison.Ordinal)) && property.Value.ValueKind == JsonValueKind.String)
                rules.Add(Rule(prop, "UtcDateTime", ".Must(value => value.Kind == DateTimeKind.Utc)", $"{prop} must be a UTC date/time.", "Date/time field should be UTC when typed as DateTime."));
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                rules.Add(Rule(prop, "NotNull", ".NotNull()", $"{prop} cannot be null.", "Array field should not be null."));
                if (required.Contains(prop))
                    rules.Add(Rule(prop, "NotEmpty", ".NotEmpty()", $"{prop} must contain at least one item.", "Required array should not be empty.", SuggestedValidationSeverity.Error));
            }
            if (property.Value.ValueKind == JsonValueKind.Object)
                InferRules(property.Value, requiredFields, rules, prop);
        }
    }

    private static string GenerateValidatorCode(string rootClassName, string? ns, IReadOnlyList<SuggestedValidationRuleDto> rules)
    {
        var sb = new StringBuilder("using FluentValidation;\n\n");
        if (!string.IsNullOrWhiteSpace(ns)) sb.Append("namespace ").Append(ns).Append(";\n\n");
        sb.Append("public sealed class ").Append(GetValidatorName(rootClassName)).Append(" : AbstractValidator<").Append(rootClassName).Append(">\n{\n");
        sb.Append("    public ").Append(GetValidatorName(rootClassName)).Append("()\n    {\n");
        foreach (var rule in rules.DistinctBy(r => (r.PropertyName, r.RuleType)))
        {
            sb.Append("        RuleFor(x => x.").Append(rule.PropertyName).Append(")\n            ").Append(rule.RuleExpression);
            if (rule.RuleType is "EmailAddress" or "AbsoluteUri") sb.Append("\n            .When(x => x.").Append(rule.PropertyName).Append(" != null)");
            sb.Append("\n            .WithMessage(\"").Append(Escape(rule.ErrorMessage)).Append("\");\n\n");
        }
        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    private static SuggestedValidationRuleDto Rule(string prop, string type, string expression, string message, string description, SuggestedValidationSeverity severity = SuggestedValidationSeverity.Warning)
        => new() { PropertyName = prop, RuleType = type, RuleExpression = expression, ErrorMessage = message, Description = description, Severity = severity };

    private static bool TryParsePayload(object? payload, out JsonDocument? document, out string? failure)
    {
        document = null; failure = null;
        try
        {
            var json = payload switch { null => string.Empty, string s => s, JsonElement e => e.GetRawText(), _ => JsonSerializer.Serialize(payload) };
            if (string.IsNullOrWhiteSpace(json)) { failure = "Payload is empty."; return false; }
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException ex) { failure = ex.Message; return false; }
    }

    private static void ValidateRequest(FluentValidationRuleGenerationRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) throw new ArgumentException("EventId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RootClassName)) throw new ArgumentException("RootClassName is required.", nameof(request));
        if (!IsValidIdentifier(request.RootClassName)) throw new ArgumentException("RootClassName must be a valid C# class name.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.Namespace) && !request.Namespace.Split('.').All(IsValidIdentifier)) throw new ArgumentException("Namespace must be a valid C# namespace.", nameof(request));
        if (request.ReceivedAtUtc != default && request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
    }

    private static void ValidateResponse(FluentValidationRuleGenerationResponseDto response)
    {
        if (response.GeneratedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("GeneratedAtUtc must be UTC.", nameof(response));
        if (response.ConfidenceScore is < 0 or > 1) throw new ArgumentException("ConfidenceScore must be between 0 and 1.", nameof(response));
    }

    private static bool IsValidIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && IdentifierRegex().IsMatch(value) && !Reserved().Contains(value);
    private static HashSet<string> Reserved() => ["class", "namespace", "public", "private", "void", "int", "string"];
    private static string GetValidatorName(string rootClassName) => rootClassName.EndsWith("Validator", StringComparison.Ordinal) ? rootClassName : rootClassName + "Validator";
    private static string ToPascal(string value) => string.Concat(Regex.Split(value, "[^A-Za-z0-9]+").Where(s => s.Length > 0).Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    private static string ValueOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static string NormalizeRisk(string? risk) => new[] { "Unknown", "Low", "Medium", "High", "Critical" }.Contains(risk) ? risk! : "Unknown";
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string RemoveSensitiveLines(string code) => string.Join('\n', code.Split('\n').Where(line => !SensitiveRegex().IsMatch(line)));
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("Authorization|Cookie|Set-Cookie|Token|Secret|Password|Api-Key|X-API-Key|ClientSecret|AccessToken|ConnectionString", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveRegex();
}
