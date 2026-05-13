using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.Fallback;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.LogSummaries;

public sealed class AiLogSummarizationService : IAiLogSummarizationService
{
    private const string MaskedValue = "[MASKED]";

    private static readonly string[] SensitiveTerms =
    [
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Token",
        "Secret",
        "Password",
        "Api-Key",
        "X-API-Key",
        "ConnectionString"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AiOptions _options;
    private readonly IAiLogSummaryPromptBuilder _promptBuilder;
    private readonly ILocalLlmClient _llmClient;
    private readonly IAiFallbackService _fallbackService;
    private readonly ILogger<AiLogSummarizationService> _logger;

    public AiLogSummarizationService(
        IOptions<AiOptions> options,
        IAiLogSummaryPromptBuilder promptBuilder,
        ILocalLlmClient llmClient,
        IAiFallbackService fallbackService,
        ILogger<AiLogSummarizationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _llmClient = llmClient;
        _fallbackService = fallbackService;
        _logger = logger;
    }

    public async Task<AiLogSummaryResponseDto> SummarizeAsync(
        AiLogSummaryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Starting AI log summarization. EventId: {EventId}, CorrelationId: {CorrelationId}, LogCount: {LogCount}",
            request.EventId,
            request.CorrelationId,
            request.Logs?.Count ?? 0);

        if (request.Logs is null || request.Logs.Count == 0)
        {
            return await CreateFallbackResponseAsync(request, AiFallbackReason.InvalidResponse, "No log entries were provided for summarization.", cancellationToken: cancellationToken);
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "AI log summarization fallback used because AI is disabled. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return await CreateFallbackResponseAsync(request, AiFallbackReason.AiDisabled, "AI is disabled; deterministic fallback log summary was used.", cancellationToken: cancellationToken);
        }

        try
        {
            var prompt = _promptBuilder.BuildPrompt(request);
            var llmResponse = await _llmClient.GenerateAsync(prompt, cancellationToken);

            if (!llmResponse.IsSuccess)
            {
                _logger.LogWarning(
                    "AI log summarization fallback used because LLM request failed. EventId: {EventId}, CorrelationId: {CorrelationId}, FallbackReason: {FallbackReason}, Provider: {Provider}, Model: {Model}, DurationMs: {DurationMs}, StatusCode: {StatusCode}",
                    request.EventId,
                    request.CorrelationId,
                    llmResponse.FallbackReason,
                    _options.Provider,
                    _options.Model,
                    llmResponse.DurationMs,
                    llmResponse.StatusCode);
                return await CreateFallbackResponseAsync(request, llmResponse.FallbackReason, llmResponse.ErrorMessage, llmResponse.DurationMs, cancellationToken);
            }

            if (!TryParseAndValidate(llmResponse.ResponseText, out var parsed, out var validationFailure))
            {
                _logger.LogWarning(
                    "AI log summary response validation failed. EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                    request.EventId,
                    request.CorrelationId,
                    validationFailure);
                var reason = validationFailure.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase) ? AiFallbackReason.InvalidJson : AiFallbackReason.InvalidResponse;
                return await CreateFallbackResponseAsync(request, reason, $"AI response could not be used: {validationFailure}", llmResponse.DurationMs, cancellationToken);
            }

            var normalized = NormalizeResponse(parsed, request);
            _logger.LogInformation(
                "AI log summarization succeeded. EventId: {EventId}, CorrelationId: {CorrelationId}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}",
                normalized.EventId,
                normalized.CorrelationId,
                normalized.RiskLevel,
                normalized.ConfidenceScore);
            return normalized;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "AI log summarization fallback used because LLM summary failed. EventId: {EventId}, CorrelationId: {CorrelationId}",
                request.EventId,
                request.CorrelationId);
            return await CreateFallbackResponseAsync(request, AiFallbackReason.UnknownError, "LLM summarization was unavailable; deterministic fallback log summary was used.", cancellationToken: cancellationToken);
        }
    }

    private static bool TryParseAndValidate(
        string llmResponse,
        out AiLogSummaryResponseDto response,
        out string failureReason)
    {
        response = new AiLogSummaryResponseDto();
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(llmResponse))
        {
            failureReason = "empty response";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(llmResponse);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                failureReason = "response root is not a JSON object";
                return false;
            }

            var requiredFields = new[]
            {
                "summary",
                "rootCause",
                "impact",
                "recommendation",
                "riskLevel",
                "confidenceScore",
                "generatedAtUtc"
            };

            foreach (var field in requiredFields)
            {
                if (!root.TryGetProperty(field, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    failureReason = $"missing required field '{field}'";
                    return false;
                }
            }

            if (!HasNonEmptyString(root, "summary") ||
                !HasNonEmptyString(root, "rootCause") ||
                !HasNonEmptyString(root, "impact") ||
                !HasNonEmptyString(root, "recommendation"))
            {
                failureReason = "one or more required text fields are empty";
                return false;
            }

            if (!TryGetEnum<AiRiskLevel>(root, "riskLevel", out _))
            {
                failureReason = "riskLevel is not a valid AiRiskLevel value";
                return false;
            }

            if (!TryGetDouble(root, "confidenceScore", out _))
            {
                failureReason = "confidenceScore is not a number";
                return false;
            }

            if (!TryGetDateTime(root, "generatedAtUtc", out _))
            {
                failureReason = "generatedAtUtc is not a valid timestamp";
                return false;
            }

            response = JsonSerializer.Deserialize<AiLogSummaryResponseDto>(llmResponse, JsonOptions) ?? new AiLogSummaryResponseDto();
            return true;
        }
        catch (JsonException)
        {
            failureReason = "invalid JSON";
            return false;
        }
    }

    private AiLogSummaryResponseDto NormalizeResponse(AiLogSummaryResponseDto response, AiLogSummaryRequestDto request)
    {
        response.EventId = request.EventId;
        response.CorrelationId = request.CorrelationId;
        response.ConfidenceScore = Math.Clamp(response.ConfidenceScore, 0, 1);
        response.GeneratedAtUtc = NormalizeTimestamp(response.GeneratedAtUtc);
        response.Model = _options.Model;
        response.Provider = _options.Provider;
        return response;
    }

    private Task<AiLogSummaryResponseDto> CreateFallbackResponseAsync(
        AiLogSummaryRequestDto request,
        AiFallbackReason reason,
        string message,
        long durationMs = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableFallback)
        {
            _logger.LogWarning(
                "AI fallback is disabled but log summary needs fallback. EventId: {EventId}, CorrelationId: {CorrelationId}, FallbackReason: {FallbackReason}",
                request.EventId,
                request.CorrelationId,
                reason);
        }

        return _fallbackService.CreateLogSummaryAsync(request, reason, message, durationMs, cancellationToken);
    }

    private static AiRiskLevel DetermineFallbackRisk(int errorCount, int warningCount)
    {
        if (errorCount >= 3)
        {
            return AiRiskLevel.High;
        }

        if (errorCount > 0)
        {
            return AiRiskLevel.Medium;
        }

        if (warningCount > 0)
        {
            return AiRiskLevel.Low;
        }

        return AiRiskLevel.Unknown;
    }

    private static string SafeFallbackText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        const int maxFallbackLength = 500;
        var sanitized = MaskSensitiveValues(value);

        return sanitized.Length <= maxFallbackLength
            ? sanitized
            : string.Concat(sanitized.AsSpan(0, maxFallbackLength), $"... [truncated from {sanitized.Length} to {maxFallbackLength} characters]");
    }

    private static string MaskSensitiveValues(string value)
    {
        var masked = value;

        foreach (var term in SensitiveTerms)
        {
            masked = SensitiveAssignmentRegex(term).Replace(masked, match =>
            {
                var key = match.Groups["key"].Value;
                var separator = match.Groups["separator"].Value;
                return $"{key}{separator}{MaskedValue}";
            });
        }

        return masked;
    }

    private static Regex SensitiveAssignmentRegex(string term)
        => new(
            $@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]""]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static bool HasNonEmptyString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryGetEnum<TEnum>(JsonElement root, string propertyName, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Enum.TryParse(property.GetString(), ignoreCase: true, out value) &&
               Enum.IsDefined(value);
    }

    private static bool TryGetDouble(JsonElement root, string propertyName, out double value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetDouble(out value) &&
               !double.IsNaN(value) &&
               !double.IsInfinity(value);
    }

    private static bool TryGetDateTime(JsonElement root, string propertyName, out DateTime value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               property.TryGetDateTime(out value);
    }

    private static DateTime NormalizeTimestamp(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool IsError(AiLogEntryDto log)
        => log.Level.Contains("error", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("fatal", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarning(AiLogEntryDto log)
        => log.Level.Contains("warn", StringComparison.OrdinalIgnoreCase);
}
