using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.PromptVersioning;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Prompts;

public sealed partial class DeadLetterAiAnalysisPromptBuilder : IDeadLetterAiAnalysisPromptBuilder
{
    public const string PromptName = "DeadLetterAiAnalysis";
    public const string PromptVersion = "v1.0.0";
    private const string MaskedValue = "[MASKED]";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static readonly string[] SensitiveTerms = ["authorization", "cookie", "token", "secret", "password", "apikey", "api-key", "x-api-key", "set-cookie"];
    private readonly DeadLetterAiAnalysisOptions _options;

    public DeadLetterAiAnalysisPromptBuilder(IOptions<DeadLetterAiAnalysisOptions> options) => _options = options.Value;

    public string BuildPrompt(DeadLetterAiAnalysisRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = new
        {
            request.DeadLetterId,
            request.EventId,
            request.CorrelationId,
            request.CustomerId,
            request.CustomerIdType,
            request.SubscriptionId,
            request.EndpointId,
            request.Environment,
            request.EventType,
            request.Source,
            request.TargetUrl,
            request.HttpMethod,
            request.StatusCode,
            request.FailureReason,
            request.ErrorMessage,
            request.RetryCount,
            request.MaxRetryCount,
            request.LastRetryAtUtc,
            request.FailedAtUtc,
            request.MovedToDeadLetterAtUtc,
            Headers = MaskHeaders(request.Headers),
            Payload = Truncate(MaskSensitiveText(SerializeValue(request.Payload)), Math.Max(1, _options.MaxPayloadLength)),
            ResponseBody = Truncate(MaskSensitiveText(request.ResponseBody ?? string.Empty), Math.Max(1, _options.MaxResponseBodyLength)),
            request.IsSuspicious,
            request.IsReplay,
            request.IsDuplicate
        };

        var contextJson = JsonSerializer.Serialize(context, JsonOptions);
        return $$"""
You are HookBridge AI, an advisory dead-letter webhook analysis assistant.

Analyze why this webhook event reached dead-letter, classify replay safety, and recommend safe next steps.

Rules:
- Use only supplied context; do not invent missing data.
- Treat masked values as unavailable and never reconstruct secrets.
- If payload or responseBody is truncated, analyze only visible content and lower confidence if relevant evidence may be hidden.
- Replay recommendations are advisory only and always require human approval before replaying or applying any production action.
- Never recommend direct replay for authentication/authorization failures or likely payload contract issues.
- confidenceScore must be between 0 and 1.
- replaySafety must be one of Unknown, SafeToReplay, ReplayWithCaution, RequiresFixBeforeReplay, DoNotReplay, RequiresManualReview.
- suggestedAction must be one of None, Replay, ReplayWithBackoff, FixPayloadBeforeReplay, FixAuthenticationBeforeReplay, FixEndpointBeforeReplay, KeepInDeadLetter, Quarantine, Reject, RequireManualReview.
- riskLevel must be one of Unknown, Low, Medium, High, Critical.

Return strict JSON only with this shape:
{
  "deadLetterId": "string",
  "eventId": "string",
  "correlationId": "string or null",
  "rootCause": "string",
  "summary": "string",
  "recommendation": "string",
  "replaySafety": "ReplayWithCaution",
  "suggestedAction": "ReplayWithBackoff",
  "riskLevel": "Medium",
  "confidenceScore": 0.82,
  "confidenceLevel": "High",
  "requiresApproval": true,
  "reasonCodes": ["RateLimited"],
  "generatedAtUtc": "2026-05-14T00:00:00Z"
}

Dead-letter context:
{{contextJson}}
""";
    }

    public Task<AiPromptBuildResult> BuildPromptWithMetadataAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(request);
        return Task.FromResult(new AiPromptBuildResult
        {
            Content = prompt,
            Metadata = new AiPromptVersionInfoDto { PromptName = PromptName, Version = PromptVersion, Hash = Hash(prompt) }
        });
    }

    private static IDictionary<string, string> MaskHeaders(IDictionary<string, string>? headers)
        => headers?.ToDictionary(pair => pair.Key, pair => IsSensitiveName(pair.Key) ? MaskedValue : MaskSensitiveText(pair.Value), StringComparer.OrdinalIgnoreCase)
           ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string SerializeValue(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(value, JsonOptions)
    };

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), $"... [truncated from {value.Length} to {maxLength} characters]");

    private static bool IsSensitiveName(string name) => SensitiveTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string MaskSensitiveText(string value)
    {
        var masked = value;
        foreach (var term in SensitiveTerms)
        {
            masked = JsonPropertyRegex(term).Replace(masked, match => $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
            masked = SensitiveAssignmentRegex(term).Replace(masked, match => $"{match.Groups["key"].Value}{match.Groups["separator"].Value}{MaskedValue}");
        }
        return masked;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static Regex JsonPropertyRegex(string term) => new($"(?<prefix>\\\"[^\\\"]*{Regex.Escape(term)}[^\\\"]*\\\"\\s*:\\s*\\\")[^\\\"]*(?<suffix>\\\")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static Regex SensitiveAssignmentRegex(string term) => new($"(?<key>{Regex.Escape(term)}[^=&\\s]*)(?<separator>\\s*[=:]\\s*)[^&\\s,;]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
