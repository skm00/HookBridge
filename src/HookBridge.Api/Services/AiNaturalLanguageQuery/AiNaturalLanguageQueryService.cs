using System.Diagnostics;
using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.PromptVersioning;
using HookBridge.AI.Worker.Services;
using HookBridge.Api.Configuration;
using HookBridge.Application.DTOs.AiNaturalLanguageQuery;
using HookBridge.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public sealed class AiNaturalLanguageQueryService(
    IAiAnalysisResultRepository analysisRepository,
    IAiAnomalyRecordRepository anomalyRepository,
    IAiSecurityAnalysisRepository securityAnalysisRepository,
    ICustomerEndpointRiskScoreRepository riskScoreRepository,
    IWebhookFailureAnomalyDetectionRepository failureAnomalyRepository,
    IAiNaturalLanguageQueryPromptBuilder promptBuilder,
    ILocalLlmClient llmClient,
    IOptions<AiNaturalLanguageQueryOptions> queryOptions,
    IOptions<AiOptions> aiOptions,
    IDateTimeProvider dateTimeProvider,
    ILogger<AiNaturalLanguageQueryService> logger) : IAiNaturalLanguageQueryService
{
    private const int QueryMaxLength = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AiNaturalLanguageQueryResponseDto> QueryAsync(AiNaturalLanguageQueryRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var normalized = NormalizeAndValidate(request);
        var intent = DetectIntent(normalized);
        var filterLog = BuildFiltersUsed(normalized);

        logger.LogInformation(
            "AI natural language query received. Intent={Intent} HasCustomerId={HasCustomerId} HasEventId={HasEventId} HasCorrelationId={HasCorrelationId} MaxResults={MaxResults}",
            intent,
            !string.IsNullOrWhiteSpace(normalized.CustomerId),
            !string.IsNullOrWhiteSpace(normalized.EventId),
            !string.IsNullOrWhiteSpace(normalized.CorrelationId),
            normalized.MaxResults);

        var results = await QueryRepositoriesAsync(normalized, intent, cancellationToken);
        var answer = await GenerateAnswerAsync(normalized, intent, results, cancellationToken);

        logger.LogInformation(
            "AI natural language query completed. Intent={Intent} ResultCount={ResultCount} DurationMs={DurationMs} Fallback={Fallback} Filters={@Filters}",
            intent,
            results.Count,
            stopwatch.ElapsedMilliseconds,
            answer.Fallback,
            filterLog);

        return new AiNaturalLanguageQueryResponseDto
        {
            Query = normalized.Query,
            Answer = answer.Answer,
            Intent = intent,
            FiltersUsed = filterLog,
            Results = results,
            SuggestedActions = answer.SuggestedActions,
            ConfidenceScore = answer.ConfidenceScore,
            GeneratedAtUtc = dateTimeProvider.UtcNow,
            Model = aiOptions.Value.Model,
            Provider = aiOptions.Value.Provider,
            Fallback = answer.Fallback,
            PromptName = answer.PromptMetadata?.PromptName ?? string.Empty,
            PromptVersion = answer.PromptMetadata?.Version ?? string.Empty,
            PromptHash = answer.PromptMetadata?.Hash ?? string.Empty
        };
    }

    internal AiNaturalLanguageQueryIntent DetectIntent(AiNaturalLanguageQueryRequestDto request)
    {
        var query = request.Query.ToLowerInvariant();
        if (query.Contains("dead letter", StringComparison.Ordinal) || query.Contains("dlq", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.DeadLetterRecommendations;
        if (query.Contains("event", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(request.EventId)) return AiNaturalLanguageQueryIntent.EventLookup;
        if (query.Contains("fail", StringComparison.Ordinal) || query.Contains("failure", StringComparison.Ordinal) || query.Contains("error", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.FailureAnalysis;
        if (query.Contains("anomaly", StringComparison.Ordinal) || query.Contains("spike", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.AnomalySearch;
        if (query.Contains("security", StringComparison.Ordinal) || query.Contains("suspicious", StringComparison.Ordinal) || query.Contains("attack", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.SecurityFindings;
        if (query.Contains("retry", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.RetryRecommendations;
        if (query.Contains("risk", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.EndpointRisk;
        if (query.Contains("health", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.EndpointHealth;
        if (query.Contains("dashboard", StringComparison.Ordinal) || query.Contains("summary", StringComparison.Ordinal)) return AiNaturalLanguageQueryIntent.DashboardSummary;
        return AiNaturalLanguageQueryIntent.Unknown;
    }

    private AiNaturalLanguageQueryRequestDto NormalizeAndValidate(AiNaturalLanguageQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new AiNaturalLanguageQueryValidationException(nameof(request.Query), "Query is required.");
        }

        if (request.Query.Length > QueryMaxLength)
        {
            throw new AiNaturalLanguageQueryValidationException(nameof(request.Query), $"Query must be {QueryMaxLength} characters or fewer.");
        }

        ValidateUtc(nameof(request.FromUtc), request.FromUtc);
        ValidateUtc(nameof(request.ToUtc), request.ToUtc);

        var options = queryOptions.Value;
        var now = dateTimeProvider.UtcNow;
        var toUtc = request.ToUtc ?? now;
        var fromUtc = request.FromUtc ?? toUtc.AddHours(-options.DefaultLookbackHours);

        if (toUtc <= fromUtc)
        {
            throw new AiNaturalLanguageQueryValidationException(nameof(request.ToUtc), "ToUtc must be greater than FromUtc.");
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(options.MaxLookbackDays))
        {
            throw new AiNaturalLanguageQueryValidationException(nameof(request.FromUtc), $"Date range must not exceed {options.MaxLookbackDays} days.");
        }

        var hardMax = Math.Max(1, options.HardMaxResults);
        var maxResults = request.MaxResults ?? options.DefaultMaxResults;
        if (maxResults < 1)
        {
            throw new AiNaturalLanguageQueryValidationException(nameof(request.MaxResults), "MaxResults must be at least 1.");
        }

        maxResults = Math.Min(maxResults, hardMax);

        return new AiNaturalLanguageQueryRequestDto
        {
            Query = request.Query.Trim(),
            Environment = TrimToNull(request.Environment),
            CustomerId = TrimToNull(request.CustomerId),
            CustomerIdType = TrimToNull(request.CustomerIdType),
            SubscriptionId = TrimToNull(request.SubscriptionId),
            EndpointId = TrimToNull(request.EndpointId),
            EventId = TrimToNull(request.EventId),
            CorrelationId = TrimToNull(request.CorrelationId),
            FromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
            MaxResults = maxResults
        };
    }

    private async Task<IReadOnlyList<AiNaturalLanguageQueryResultDto>> QueryRepositoriesAsync(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, CancellationToken cancellationToken)
    {
        var results = new List<AiNaturalLanguageQueryResultDto>();
        var limit = request.MaxResults!.Value;

        if (!string.IsNullOrWhiteSpace(request.EventId))
        {
            var analysis = await analysisRepository.GetByEventIdAsync(request.EventId, cancellationToken);
            if (analysis is not null) results.Add(MapAnalysis(analysis));
            var security = await securityAnalysisRepository.GetByEventIdAsync(request.EventId, cancellationToken);
            if (security is not null) results.Add(MapSecurity(security));
            results.AddRange((await anomalyRepository.GetByEventIdAsync(request.EventId, cancellationToken)).Select(MapAnomaly));
        }
        else if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            results.AddRange((await analysisRepository.GetByCorrelationIdAsync(request.CorrelationId, cancellationToken)).Select(MapAnalysis));
            results.AddRange((await securityAnalysisRepository.GetByCorrelationIdAsync(request.CorrelationId, cancellationToken)).Select(MapSecurity));
            results.AddRange((await anomalyRepository.GetByCorrelationIdAsync(request.CorrelationId, cancellationToken)).Select(MapAnomaly));
        }
        else
        {
            switch (intent)
            {
                case AiNaturalLanguageQueryIntent.AnomalySearch:
                    results.AddRange((await anomalyRepository.SearchAsync(ToAnomalySearch(request), cancellationToken)).Select(MapAnomaly));
                    results.AddRange((await failureAnomalyRepository.GetAnomaliesAsync(null, limit, cancellationToken)).Select(MapFailureAnomaly));
                    break;
                case AiNaturalLanguageQueryIntent.SecurityFindings:
                    results.AddRange((await securityAnalysisRepository.SearchAsync(ToSecuritySearch(request), cancellationToken)).Select(MapSecurity));
                    break;
                case AiNaturalLanguageQueryIntent.EndpointRisk:
                case AiNaturalLanguageQueryIntent.EndpointHealth:
                    results.AddRange((await QueryRiskScoresAsync(request, limit, cancellationToken)).Select(MapRisk));
                    break;
                case AiNaturalLanguageQueryIntent.RetryRecommendations:
                case AiNaturalLanguageQueryIntent.DeadLetterRecommendations:
                case AiNaturalLanguageQueryIntent.FailureAnalysis:
                case AiNaturalLanguageQueryIntent.EventLookup:
                case AiNaturalLanguageQueryIntent.DashboardSummary:
                case AiNaturalLanguageQueryIntent.Unknown:
                default:
                    results.AddRange((await analysisRepository.GetRecentAsync(limit, cancellationToken)).Select(MapAnalysis));
                    if (intent is AiNaturalLanguageQueryIntent.FailureAnalysis or AiNaturalLanguageQueryIntent.DashboardSummary or AiNaturalLanguageQueryIntent.Unknown)
                    {
                        results.AddRange((await failureAnomalyRepository.GetRecentAsync(limit, cancellationToken)).Select(MapFailureAnomaly));
                    }
                    break;
            }
        }

        return results
            .Where(result => MatchesFilters(result, request))
            .OrderByDescending(result => result.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    private async Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> QueryRiskScoresAsync(AiNaturalLanguageQueryRequestDto request, int limit, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) return await riskScoreRepository.GetByEndpointIdAsync(request.EndpointId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) return await riskScoreRepository.GetBySubscriptionIdAsync(request.SubscriptionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) return await riskScoreRepository.GetByCustomerIdAsync(request.CustomerId, cancellationToken);
        return await riskScoreRepository.GetRecentAsync(limit, cancellationToken);
    }

    private async Task<AnswerResult> GenerateAnswerAsync(AiNaturalLanguageQueryRequestDto request, AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results, CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return BuildFallbackAnswer(intent, results, fallback: true);
        }

        if (!queryOptions.Value.EnableAiAnswerGeneration || !aiOptions.Value.Enabled)
        {
            return BuildFallbackAnswer(intent, results, fallback: true);
        }

        try
        {
            var promptResult = await promptBuilder.BuildPromptWithMetadataAsync(request, intent, results, cancellationToken);
            var llmResponse = await llmClient.GenerateAsync(promptResult.Content, cancellationToken);
            if (!llmResponse.IsSuccess || string.IsNullOrWhiteSpace(llmResponse.ResponseText))
            {
                return BuildFallbackAnswer(intent, results, fallback: true);
            }

            var parsed = JsonSerializer.Deserialize<AiAnswerJson>(llmResponse.ResponseText, JsonOptions);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Answer))
            {
                return BuildFallbackAnswer(intent, results, fallback: true);
            }

            return new AnswerResult(
                parsed.Answer.Trim(),
                parsed.SuggestedActions?.Where(action => !string.IsNullOrWhiteSpace(action)).Select(action => action.Trim()).Take(5).ToList() ?? SafeDefaultActions(intent, results),
                Math.Clamp(parsed.ConfidenceScore, 0, 1),
                false,
                promptResult.Metadata);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI natural language answer generation failed; deterministic fallback will be used.");
            return BuildFallbackAnswer(intent, results, fallback: true);
        }
    }

    private AnswerResult BuildFallbackAnswer(AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results, bool fallback)
    {
        if (results.Count == 0)
        {
            return new AnswerResult("No matching AI analysis, anomaly, endpoint risk, or security finding data was found for the requested filters.", SafeDefaultActions(intent, results), 0.45, fallback);
        }

        var highestRisk = results.OrderByDescending(RiskRank).First();
        var answer = $"Found {results.Count} matching result(s) for {intent}. Highest observed risk is {NormalizeDisplay(highestRisk.RiskLevel)}: {highestRisk.Title}. {highestRisk.Summary}";
        return new AnswerResult(answer, SafeDefaultActions(intent, results), Math.Min(0.75, 0.5 + results.Count * 0.02), fallback);
    }

    private static IReadOnlyList<string> SafeDefaultActions(AiNaturalLanguageQueryIntent intent, IReadOnlyList<AiNaturalLanguageQueryResultDto> results)
    {
        if (results.Count == 0) return ["Verify the filters and widen the UTC time window if appropriate."];
        return intent switch
        {
            AiNaturalLanguageQueryIntent.RetryRecommendations => ["Review retry recommendations before replaying events.", "Use exponential backoff for transient failures."],
            AiNaturalLanguageQueryIntent.DeadLetterRecommendations => ["Review repeated or high-risk failures before moving events to dead letter.", "Confirm downstream endpoint health before replay."],
            AiNaturalLanguageQueryIntent.SecurityFindings => ["Review suspicious events and authentication failures.", "Rotate exposed credentials if any external evidence indicates compromise."],
            AiNaturalLanguageQueryIntent.EndpointRisk or AiNaturalLanguageQueryIntent.EndpointHealth => ["Review high-risk endpoints and reduce delivery concurrency if failures are elevated.", "Confirm endpoint availability before replay."],
            _ => ["Review the highest-risk results first.", "Retry only after confirming the downstream endpoint can accept traffic."]
        };
    }

    private static bool MatchesFilters(AiNaturalLanguageQueryResultDto result, AiNaturalLanguageQueryRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.EventId) && !string.Equals(result.EventId, request.EventId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && !string.Equals(result.CorrelationId, request.CorrelationId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.Environment) && !string.Equals(result.Environment, request.Environment, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.CustomerId) && !string.Equals(result.CustomerId, request.CustomerId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.CustomerIdType) && !string.Equals(result.CustomerIdType, request.CustomerIdType, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId) && !string.Equals(result.SubscriptionId, request.SubscriptionId, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.EndpointId) && !string.Equals(result.EndpointId, request.EndpointId, StringComparison.OrdinalIgnoreCase)) return false;
        return result.CreatedAtUtc >= request.FromUtc!.Value && result.CreatedAtUtc <= request.ToUtc!.Value;
    }

    private static AiAnomalyRecordSearchRequestDto ToAnomalySearch(AiNaturalLanguageQueryRequestDto request) => new()
    {
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        FromUtc = request.FromUtc,
        ToUtc = request.ToUtc,
        PageSize = request.MaxResults!.Value
    };

    private static AiSecurityAnalysisSearchRequestDto ToSecuritySearch(AiNaturalLanguageQueryRequestDto request) => new()
    {
        CustomerId = request.CustomerId,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        FromUtc = request.FromUtc,
        ToUtc = request.ToUtc,
        Limit = request.MaxResults!.Value
    };

    private static AiNaturalLanguageQueryResultDto MapAnalysis(AiAnalysisResult result) => new()
    {
        Id = result.Id,
        EventId = result.EventId,
        CorrelationId = result.CorrelationId,
        CustomerId = result.CustomerId,
        CustomerIdType = result.CustomerIdType,
        Environment = result.Environment,
        SubscriptionId = result.SubscriptionId,
        EndpointId = result.EndpointId,
        ResultType = "FailureAnalysis",
        Title = string.IsNullOrWhiteSpace(result.FailureReason) ? "Webhook failure analysis" : result.FailureReason,
        Summary = FirstNonEmpty(result.AiSummary, result.RootCause, result.AiRecommendation),
        RiskLevel = result.RiskLevel,
        SuggestedAction = result.SuggestedRetryAction,
        CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc)
    };

    private static AiNaturalLanguageQueryResultDto MapAnomaly(AiAnomalyRecord result) => new()
    {
        Id = result.Id ?? result.AnomalyId,
        EventId = result.EventId,
        CorrelationId = result.CorrelationId,
        CustomerId = result.CustomerId,
        CustomerIdType = result.CustomerIdType,
        Environment = result.Environment,
        SubscriptionId = result.SubscriptionId,
        EndpointId = result.EndpointId,
        ResultType = "Anomaly",
        Title = string.IsNullOrWhiteSpace(result.AnomalyType) ? "Anomaly detected" : result.AnomalyType,
        Summary = result.Summary,
        RiskLevel = result.RiskLevel,
        SuggestedAction = result.Recommendation,
        CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc)
    };

    private static AiNaturalLanguageQueryResultDto MapSecurity(AiSecurityAnalysisResult result) => new()
    {
        Id = result.Id,
        EventId = result.EventId,
        CorrelationId = result.CorrelationId,
        CustomerId = result.CustomerId,
        CustomerIdType = result.CustomerIdType,
        Environment = result.Environment,
        SubscriptionId = result.SubscriptionId,
        EndpointId = result.EndpointId,
        ResultType = "SecurityFinding",
        Title = result.IsSuspicious ? "Suspicious webhook activity" : "Security analysis",
        Summary = result.Summary,
        RiskLevel = result.RiskLevel,
        SuggestedAction = result.SuggestedAction,
        CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc == default ? result.GeneratedAtUtc : result.CreatedAtUtc, DateTimeKind.Utc)
    };

    private static AiNaturalLanguageQueryResultDto MapRisk(CustomerEndpointRiskScoreResult result) => new()
    {
        Id = result.Id,
        CustomerId = result.CustomerId,
        CustomerIdType = result.CustomerIdType,
        Environment = result.Environment,
        SubscriptionId = result.SubscriptionId,
        EndpointId = result.EndpointId,
        ResultType = "EndpointRisk",
        Title = $"Endpoint risk: {result.RiskLevel}",
        Summary = string.IsNullOrWhiteSpace(result.Summary) ? result.HealthStatus : result.Summary,
        RiskLevel = result.RiskLevel,
        SuggestedAction = result.Recommendation,
        CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc == default ? result.CalculatedAtUtc : result.CreatedAtUtc, DateTimeKind.Utc)
    };

    private static AiNaturalLanguageQueryResultDto MapFailureAnomaly(WebhookFailureAnomalyDetectionResult result) => new()
    {
        Id = result.Id,
        CustomerId = result.CustomerId,
        CustomerIdType = result.CustomerIdType,
        Environment = result.Environment,
        SubscriptionId = result.SubscriptionId,
        EndpointId = result.EndpointId,
        ResultType = "FailureAnomaly",
        Title = result.IsAnomalyDetected ? "Failure anomaly detected" : "Failure anomaly evaluation",
        Summary = result.Summary,
        RiskLevel = result.RiskLevel,
        SuggestedAction = result.Recommendation,
        CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc == default ? result.CalculatedAtUtc : result.CreatedAtUtc, DateTimeKind.Utc)
    };

    private Dictionary<string, object?> BuildFiltersUsed(AiNaturalLanguageQueryRequestDto request) => new()
    {
        ["environment"] = request.Environment,
        ["customerId"] = request.CustomerId,
        ["customerIdType"] = request.CustomerIdType,
        ["subscriptionId"] = request.SubscriptionId,
        ["endpointId"] = request.EndpointId,
        ["eventId"] = request.EventId,
        ["correlationId"] = request.CorrelationId,
        ["fromUtc"] = request.FromUtc,
        ["toUtc"] = request.ToUtc,
        ["maxResults"] = request.MaxResults
    };

    private static void ValidateUtc(string fieldName, DateTime? value)
    {
        if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
        {
            throw new AiNaturalLanguageQueryValidationException(fieldName, $"{fieldName} must be UTC.");
        }
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string NormalizeDisplay(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    private static int RiskRank(AiNaturalLanguageQueryResultDto result) => result.RiskLevel?.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        "low" => 1,
        _ => 0
    };

    private sealed record AnswerResult(string Answer, IReadOnlyList<string> SuggestedActions, double ConfidenceScore, bool Fallback, AiPromptVersionInfoDto? PromptMetadata = null);
    private sealed class AiAnswerJson
    {
        public string Answer { get; set; } = string.Empty;
        public List<string>? SuggestedActions { get; set; }
        public double ConfidenceScore { get; set; }
    }
}
