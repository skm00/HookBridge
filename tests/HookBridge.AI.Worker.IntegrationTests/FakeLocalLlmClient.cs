using System.Text.Json;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services;

namespace HookBridge.AI.Worker.IntegrationTests;

public enum FakeLocalLlmMode
{
    Success,
    Timeout,
    InvalidJson,
    ProviderUnavailable
}

public sealed class FakeLocalLlmClient : ILocalLlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FakeLocalLlmMode Mode { get; set; } = FakeLocalLlmMode.Success;

    public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Mode switch
        {
            FakeLocalLlmMode.Timeout => Task.FromResult(LlmResponseResult.Failure(AiFallbackReason.Timeout, "The fake LLM timed out.", 25, 408)),
            FakeLocalLlmMode.InvalidJson => Task.FromResult(LlmResponseResult.Success("{not valid json", 5)),
            FakeLocalLlmMode.ProviderUnavailable => Task.FromResult(LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "The fake LLM provider is unavailable.", 5, 503)),
            _ => Task.FromResult(LlmResponseResult.Success(CreateDeterministicResponse(prompt), 5))
        };
    }

    private static string CreateDeterministicResponse(string prompt)
    {
        if (prompt.Contains("security", StringComparison.OrdinalIgnoreCase) || prompt.Contains("suspicious", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                eventId = "fake-security-event",
                correlationId = "fake-security-correlation",
                isSuspicious = true,
                securityRiskScore = 90,
                riskLevel = "Critical",
                summary = "Suspicious webhook content detected by fake local LLM.",
                recommendation = "Quarantine and manually review this webhook before replay.",
                detectedSecuritySignals = new[]
                {
                    new
                    {
                        signalName = "ScriptContent",
                        severity = "High",
                        description = "Script-like content was detected.",
                        evidence = "redacted",
                        recommendation = "Quarantine before replay."
                    }
                },
                suggestedAction = "Quarantine",
                confidenceScore = 0.91,
                generatedAtUtc = DateTime.UtcNow,
                model = "fake-llm",
                provider = "fake"
            }, JsonOptions);
        }

        var statusCode = ExtractStatusCode(prompt);
        var reachedMaxRetry = ContainsIntegerField(prompt, "retryCount", 5) && ContainsIntegerField(prompt, "maxRetryCount", 5);
        var action = reachedMaxRetry || statusCode == 404
            ? "MoveToDeadLetter"
            : statusCode is 401 or 403
                ? "RequireManualReview"
                : "RetryWithBackoff";
        var risk = reachedMaxRetry ? "Critical" : statusCode switch
        {
            429 => "Medium",
            500 or 502 or 503 => "High",
            401 or 403 or 404 => "High",
            408 or 504 => "Medium",
            _ => "Medium"
        };
        var retry = action == "RetryWithBackoff";
        var summary = statusCode switch
        {
            429 => "The receiver returned a rate limit response and should be retried with backoff.",
            500 or 502 or 503 => "The receiver reported a server-side failure or receiver error.",
            401 or 403 => "The receiver rejected the delivery because authentication credentials failed.",
            404 => "The endpoint URL appears missing or points to a deleted resource.",
            408 or 504 => "The receiver timed out or was unavailable during delivery.",
            _ => "The webhook delivery failed and requires review."
        };

        return JsonSerializer.Serialize(new
        {
            eventId = "fake-event",
            correlationId = "fake-correlation",
            aiSummary = summary,
            rootCause = summary,
            aiRecommendation = BuildRecommendation(statusCode, action),
            riskLevel = risk,
            confidenceScore = 0.87,
            suggestedRetryAction = action,
            isRetryRecommended = retry,
            generatedAtUtc = DateTime.UtcNow,
            model = "fake-llm",
            provider = "fake"
        }, JsonOptions);
    }

    private static bool ContainsIntegerField(string prompt, string camelCaseFieldName, int expectedValue)
    {
        var pascalCaseFieldName = char.ToUpperInvariant(camelCaseFieldName[0]) + camelCaseFieldName[1..];
        return ContainsFieldValue(prompt, $"\"{camelCaseFieldName}\"", expectedValue) ||
               ContainsFieldValue(prompt, pascalCaseFieldName, expectedValue);
    }

    private static bool ContainsFieldValue(string prompt, string fieldName, int expectedValue)
        => prompt.Contains($"{fieldName}: {expectedValue}", StringComparison.OrdinalIgnoreCase) ||
           prompt.Contains($"{fieldName}:{expectedValue}", StringComparison.OrdinalIgnoreCase);

    private static int? ExtractStatusCode(string prompt)
    {
        foreach (var code in new[] { 429, 500, 503, 504, 408, 401, 403, 404, 400 })
        {
            if (prompt.Contains(code.ToString(), StringComparison.Ordinal))
            {
                return code;
            }
        }

        return null;
    }

    private static string BuildRecommendation(int? statusCode, string action) => statusCode switch
    {
        429 => "Retry with exponential backoff and honor receiver rate limits.",
        500 or 502 or 503 => "Retry with exponential backoff after the receiver server-side failure clears.",
        401 or 403 => "Require manual review of authentication credentials before retrying.",
        404 => "Review the endpoint URL or missing resource before retrying; move to dead letter if stale.",
        408 or 504 => "Retry with backoff because the timeout may indicate temporary receiver availability issues.",
        _ when action == "MoveToDeadLetter" => "Move to dead letter after max retry count was reached.",
        _ => "Require manual review before retrying."
    };
}
