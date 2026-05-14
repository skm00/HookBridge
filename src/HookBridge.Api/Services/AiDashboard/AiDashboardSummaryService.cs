using System.Diagnostics;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Configuration;
using HookBridge.Application.DTOs.AiDashboard;
using HookBridge.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace HookBridge.Api.Services.AiDashboard;

public sealed class AiDashboardSummaryService(
    IAiAnalysisResultRepository analysisRepository,
    IAiAnomalyRecordRepository anomalyRepository,
    IAiSecurityAnalysisRepository securityAnalysisRepository,
    ICustomerEndpointRiskScoreRepository riskScoreRepository,
    IDateTimeProvider dateTimeProvider,
    IOptions<AiDashboardOptions> options,
    ILogger<AiDashboardSummaryService> logger) : IAiDashboardSummaryService
{
    public async Task<AiDashboardSummaryResponseDto> GetSummaryAsync(
        AiDashboardSummaryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = NormalizeRequest(request);
        var filter = new AiDashboardQueryFilter
        {
            Environment = normalized.Environment,
            CustomerId = normalized.CustomerId,
            CustomerIdType = normalized.CustomerIdType,
            SubscriptionId = normalized.SubscriptionId,
            EndpointId = normalized.EndpointId,
            EventType = normalized.EventType,
            FromUtc = normalized.FromUtc!.Value,
            ToUtc = normalized.ToUtc!.Value
        };

        logger.LogInformation(
            "AI dashboard summary request received. Environment={Environment} CustomerIdPresent={CustomerIdPresent} CustomerIdTypePresent={CustomerIdTypePresent} SubscriptionIdPresent={SubscriptionIdPresent} EndpointIdPresent={EndpointIdPresent} EventType={EventType} FromUtc={FromUtc} ToUtc={ToUtc}",
            filter.Environment,
            !string.IsNullOrWhiteSpace(filter.CustomerId),
            !string.IsNullOrWhiteSpace(filter.CustomerIdType),
            !string.IsNullOrWhiteSpace(filter.SubscriptionId),
            !string.IsNullOrWhiteSpace(filter.EndpointId),
            filter.EventType,
            filter.FromUtc,
            filter.ToUtc);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var totalAnalyses = await analysisRepository.CountByDateRangeAsync(filter, cancellationToken);
            var totalAnomalies = await anomalyRepository.CountByDateRangeAsync(filter, cancellationToken);
            var totalSecurityFindings = await securityAnalysisRepository.CountByDateRangeAsync(filter, cancellationToken);
            var totalHighRiskEndpoints = await riskScoreRepository.CountHighRiskEndpointsAsync(filter, cancellationToken);

            var riskDistribution = MergeRiskDistributions(
                await analysisRepository.CountByRiskLevelAsync(filter, cancellationToken),
                await anomalyRepository.CountByRiskLevelAsync(filter, cancellationToken),
                await securityAnalysisRepository.CountByRiskLevelAsync(filter, cancellationToken));

            var anomalyTypes = await anomalyRepository.CountByAnomalyTypeAsync(filter, cancellationToken);
            var retryActions = await analysisRepository.CountByRetryActionAsync(filter, cancellationToken);
            var endpointHealth = await riskScoreRepository.CountByHealthStatusAsync(filter, cancellationToken);
            var analysisAverage = await analysisRepository.GetAverageConfidenceScoreAsync(filter, cancellationToken);
            var securityAverage = await securityAnalysisRepository.GetAverageConfidenceScoreAsync(filter, cancellationToken);
            var recentFindings = await GetRecentFindingsAsync(filter, options.Value.RecentFindingsLimit, cancellationToken);

            var response = new AiDashboardSummaryResponseDto
            {
                Environment = normalized.Environment,
                CustomerId = normalized.CustomerId,
                FromUtc = filter.FromUtc,
                ToUtc = filter.ToUtc,
                TotalAiAnalyses = totalAnalyses,
                TotalAnomalies = totalAnomalies,
                TotalSecurityFindings = totalSecurityFindings,
                TotalHighRiskEndpoints = totalHighRiskEndpoints,
                TotalRetryRecommendations = retryActions.Where(item => IsRetryAction(item.Key)).Sum(item => item.Value),
                TotalDeadLetterRecommendations = retryActions.Where(item => IsDeadLetterAction(item.Key)).Sum(item => item.Value),
                AverageConfidenceScore = AverageNonZero(analysisAverage, securityAverage),
                RiskDistribution = riskDistribution,
                AnomalyTypeDistribution = ToDistribution(anomalyTypes),
                RetryActionDistribution = ToDistribution(retryActions),
                EndpointHealthDistribution = ToDistribution(endpointHealth),
                RecentFindings = recentFindings,
                GeneratedAtUtc = dateTimeProvider.UtcNow
            };

            logger.LogInformation(
                "AI dashboard summary generated. DurationMs={DurationMs} TotalAiAnalyses={TotalAiAnalyses} TotalAnomalies={TotalAnomalies} TotalSecurityFindings={TotalSecurityFindings}",
                stopwatch.ElapsedMilliseconds,
                response.TotalAiAnalyses,
                response.TotalAnomalies,
                response.TotalSecurityFindings);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not AiDashboardValidationException)
        {
            logger.LogError(
                ex,
                "AI dashboard repository failure. Environment={Environment} CustomerIdPresent={CustomerIdPresent} SubscriptionIdPresent={SubscriptionIdPresent} EndpointIdPresent={EndpointIdPresent} EventType={EventType}",
                filter.Environment,
                !string.IsNullOrWhiteSpace(filter.CustomerId),
                !string.IsNullOrWhiteSpace(filter.SubscriptionId),
                !string.IsNullOrWhiteSpace(filter.EndpointId),
                filter.EventType);
            throw;
        }
    }

    private AiDashboardSummaryRequestDto NormalizeRequest(AiDashboardSummaryRequestDto request)
    {
        var now = dateTimeProvider.UtcNow;
        var toUtc = request.ToUtc ?? now;
        var fromUtc = request.FromUtc ?? toUtc.AddHours(-options.Value.DefaultLookbackHours);

        ValidateUtc(nameof(request.FromUtc), request.FromUtc);
        ValidateUtc(nameof(request.ToUtc), request.ToUtc);

        if (toUtc <= fromUtc)
        {
            throw new AiDashboardValidationException(nameof(request.ToUtc), "ToUtc must be greater than FromUtc.");
        }

        if (toUtc - fromUtc > TimeSpan.FromDays(options.Value.MaxLookbackDays))
        {
            throw new AiDashboardValidationException(nameof(request.FromUtc), $"Date range must not exceed {options.Value.MaxLookbackDays} days.");
        }

        return new AiDashboardSummaryRequestDto
        {
            Environment = TrimToNull(request.Environment),
            CustomerId = TrimToNull(request.CustomerId),
            CustomerIdType = TrimToNull(request.CustomerIdType),
            SubscriptionId = TrimToNull(request.SubscriptionId),
            EndpointId = TrimToNull(request.EndpointId),
            EventType = TrimToNull(request.EventType),
            FromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)
        };
    }

    private async Task<IReadOnlyList<AiDashboardRecentFindingDto>> GetRecentFindingsAsync(
        AiDashboardQueryFilter filter,
        int configuredLimit,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(configuredLimit, 1, 100);
        var perRepositoryLimit = limit;
        var findings = new List<AiDashboardRecentFindingResult>();
        findings.AddRange(await analysisRepository.GetRecentFindingsAsync(filter, perRepositoryLimit, cancellationToken));
        findings.AddRange(await anomalyRepository.GetRecentFindingsAsync(filter, perRepositoryLimit, cancellationToken));
        findings.AddRange(await securityAnalysisRepository.GetRecentFindingsAsync(filter, perRepositoryLimit, cancellationToken));

        return findings
            .OrderByDescending(finding => finding.CreatedAtUtc)
            .Take(limit)
            .Select(finding => new AiDashboardRecentFindingDto
            {
                Id = finding.Id,
                EventId = finding.EventId,
                CorrelationId = finding.CorrelationId,
                CustomerId = finding.CustomerId,
                SubscriptionId = finding.SubscriptionId,
                EndpointId = finding.EndpointId,
                FindingType = finding.FindingType,
                Title = finding.Title,
                Summary = finding.Summary,
                RiskLevel = finding.RiskLevel,
                SuggestedAction = finding.SuggestedAction,
                CreatedAtUtc = DateTime.SpecifyKind(finding.CreatedAtUtc, DateTimeKind.Utc)
            })
            .ToList();
    }

    private static AiDashboardRiskDistributionDto MergeRiskDistributions(params IReadOnlyDictionary<string, long>[] distributions)
    {
        var result = new AiDashboardRiskDistributionDto();
        foreach (var distribution in distributions)
        {
            foreach (var item in distribution)
            {
                switch (NormalizeRiskLevel(item.Key))
                {
                    case AiRiskLevel.Low:
                        result.Low += item.Value;
                        break;
                    case AiRiskLevel.Medium:
                        result.Medium += item.Value;
                        break;
                    case AiRiskLevel.High:
                        result.High += item.Value;
                        break;
                    case AiRiskLevel.Critical:
                        result.Critical += item.Value;
                        break;
                    default:
                        result.Unknown += item.Value;
                        break;
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<AiDashboardDistributionItemDto> ToDistribution(IReadOnlyDictionary<string, long> counts)
    {
        var total = counts.Values.Sum();
        return counts
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AiDashboardDistributionItemDto
            {
                Name = string.IsNullOrWhiteSpace(item.Key) ? "Unknown" : item.Key,
                Count = item.Value,
                Percentage = total == 0 ? 0 : Math.Round(item.Value * 100d / total, 2)
            })
            .ToList();
    }

    private static double AverageNonZero(params double[] scores)
    {
        var nonZeroScores = scores.Where(score => score > 0).ToArray();
        return nonZeroScores.Length == 0 ? 0 : Math.Round(nonZeroScores.Average(), 4);
    }

    private static bool IsRetryAction(string action)
        => string.Equals(action, SuggestedRetryAction.RetryImmediately.ToString(), StringComparison.OrdinalIgnoreCase)
           || string.Equals(action, SuggestedRetryAction.RetryWithBackoff.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsDeadLetterAction(string action)
        => string.Equals(action, SuggestedRetryAction.MoveToDeadLetter.ToString(), StringComparison.OrdinalIgnoreCase);

    private static AiRiskLevel NormalizeRiskLevel(string? value)
        => Enum.TryParse<AiRiskLevel>(value, ignoreCase: true, out var riskLevel) ? riskLevel : AiRiskLevel.Unknown;

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateUtc(string fieldName, DateTime? value)
    {
        if (value is not null && value.Value.Kind != DateTimeKind.Utc)
        {
            throw new AiDashboardValidationException(fieldName, $"{fieldName} must be UTC.");
        }
    }
}
