using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed class AiLogSummaryPromptBuilder : IAiLogSummaryPromptBuilder
{
    private const string MaskedValue = "[MASKED]";
    private const string NotProvidedValue = "[not provided]";

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
        WriteIndented = true
    };

    private readonly AiOptions _options;

    public AiLogSummaryPromptBuilder(IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public string BuildPrompt(AiLogSummaryRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxEntries = Math.Max(1, _options.MaxLogEntriesForSummary);
        var logs = request.Logs ?? Array.Empty<AiLogEntryDto>();
        var selectedLogs = logs
            .OrderBy(log => NormalizeTimestamp(log.TimestampUtc))
            .ThenBy(log => log.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(log => log.Level, StringComparer.OrdinalIgnoreCase)
            .Take(maxEntries)
            .Select(log => new
            {
                timestampUtc = NormalizeTimestamp(log.TimestampUtc),
                level = ValueOrNotProvided(log.Level),
                serviceName = ValueOrNotProvided(log.ServiceName),
                message = SanitizeAndTruncate(log.Message),
                exception = SanitizeAndTruncate(log.Exception),
                traceId = ValueOrNotProvided(log.TraceId),
                spanId = ValueOrNotProvided(log.SpanId)
            })
            .ToArray();

        var context = new
        {
            eventId = ValueOrNotProvided(request.EventId),
            correlationId = ValueOrNotProvided(request.CorrelationId),
            source = ValueOrNotProvided(request.Source),
            environment = ValueOrNotProvided(request.Environment),
            fromUtc = request.FromUtc.HasValue ? NormalizeTimestamp(request.FromUtc.Value) : (DateTime?)null,
            toUtc = request.ToUtc.HasValue ? NormalizeTimestamp(request.ToUtc.Value) : (DateTime?)null,
            totalLogCount = logs.Count,
            includedLogCount = selectedLogs.Length,
            omittedLogCount = Math.Max(0, logs.Count - selectedLogs.Length),
            errorCount = logs.Count(IsError),
            warningCount = logs.Count(IsWarning),
            logs = selectedLogs
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);
        var riskLevels = string.Join(", ", Enum.GetNames<AiRiskLevel>());

        return $$"""
You are HookBridge AI, an assistant for webhook operations debugging and support.

Summarize the webhook-related logs into a short, actionable explanation. Focus on the most likely failure path, what changed for the operator, and the next safe debugging step.

Important context rules:
- Use only the included log context; do not invent details.
- Prioritize Error and Warning entries, exceptions, HTTP status hints, retries, queueing, rate limits, authentication, and endpoint availability.
- Keep the output concise and useful for support handoff.
- AI output is a recommendation only and must not trigger production actions automatically.

Safety and data handling rules:
- Do not expose secrets or sensitive values.
- Treat masked values as unavailable and never reconstruct them.
- Sensitive fields/values include Authorization, Cookie, Set-Cookie, Token, Secret, Password, Api-Key, X-API-Key, and ConnectionString.
- Use riskLevel only from: {{riskLevels}}.
- confidenceScore must be a number between 0 and 1.
- generatedAtUtc must be the UTC time when you generate the summary in ISO 8601 format.

Return strict JSON only. Do not include markdown, prose, comments, or code fences.
The JSON object must match this exact shape and property names:
{
  "eventId": "string",
  "correlationId": "string or null",
  "summary": "string",
  "rootCause": "string",
  "impact": "string",
  "recommendation": "string",
  "riskLevel": "Unknown|Low|Medium|High|Critical",
  "confidenceScore": 0.0,
  "generatedAtUtc": "2026-05-13T00:00:00Z"
}

Webhook log context:
{{contextJson}}
""";
    }

    private static DateTime NormalizeTimestamp(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string ValueOrNotProvided(string? value)
        => string.IsNullOrWhiteSpace(value) ? NotProvidedValue : value;

    private string SanitizeAndTruncate(string? value)
    {
        var normalizedValue = ValueOrNotProvided(value);
        var sanitized = _options.MaskSensitiveValues ? MaskSensitiveValues(normalizedValue) : normalizedValue;
        var maxLength = Math.Max(1, _options.MaxLogMessageLength);

        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return string.Concat(
            sanitized.AsSpan(0, maxLength),
            $"... [truncated from {sanitized.Length} to {maxLength} characters]");
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

    private static bool IsError(AiLogEntryDto log)
        => log.Level.Contains("error", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
           log.Level.Contains("fatal", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarning(AiLogEntryDto log)
        => log.Level.Contains("warn", StringComparison.OrdinalIgnoreCase);

    private static Regex SensitiveAssignmentRegex(string term)
        => new(
            $@"(?<key>\b{Regex.Escape(term)}\b)(?<separator>\s*(?:=|:|=>)\s*""?)[^\r\n,}}\]""]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
}
