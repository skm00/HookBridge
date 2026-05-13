using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.EndpointHealthScoring;

/// <summary>
/// Pure, deterministic endpoint health scorer. It does not call Kafka, MongoDB, Ollama, or any external API.
/// </summary>
public sealed class EndpointHealthScoringService : IEndpointHealthScoringService
{
    private const double MaximumFailureRatePenalty = 50;
    private const double AverageLatencyThresholdMs = 1_000;
    private const double P95LatencyThresholdMs = 2_000;

    public EndpointHealthScoreResponseDto CalculateHealthScore(
        EndpointHealthScoreRequestDto request,
        DateTime calculatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(calculatedAtUtc, nameof(calculatedAtUtc));
        ValidateRequest(request);

        if (request.TotalDeliveries == 0)
        {
            return CreateResponse(
                request,
                healthScore: 0,
                EndpointHealthStatus.Unknown,
                calculatedAtUtc,
                "Endpoint health is unknown because there are no deliveries in the evaluation window.",
                "Collect delivery data before making endpoint health decisions.");
        }

        var score = 100d;
        score -= CalculateFailureRatePenalty(request);
        score -= Math.Min(15, request.TimeoutCount * 2d);
        score -= Math.Min(15, request.RateLimitCount * 3d);
        score -= Math.Min(20, request.ServerErrorCount * 3d);
        score -= Math.Min(15, request.ClientErrorCount * 2d);
        score -= Math.Min(10, request.RetryCount);
        score -= CalculateAverageLatencyPenalty(request.AverageLatencyMs);
        score -= CalculateP95LatencyPenalty(request.P95LatencyMs);
        score -= Math.Min(25, request.DeadLetterCount * 10d);
        score -= CalculateRecentFailurePenalty(request, calculatedAtUtc);

        var healthScore = (int)Math.Clamp(Math.Round(score, MidpointRounding.AwayFromZero), 0, 100);
        var healthStatus = MapHealthStatus(healthScore);

        return CreateResponse(
            request,
            healthScore,
            healthStatus,
            calculatedAtUtc,
            BuildSummary(healthStatus, request),
            BuildRecommendation(request));
    }

    private static double CalculateFailureRatePenalty(EndpointHealthScoreRequestDto request)
    {
        var failureRate = request.TotalDeliveries == 0
            ? 0
            : (double)request.FailedDeliveries / request.TotalDeliveries;

        return Math.Min(MaximumFailureRatePenalty, failureRate * MaximumFailureRatePenalty);
    }

    private static double CalculateAverageLatencyPenalty(double averageLatencyMs)
    {
        if (averageLatencyMs <= AverageLatencyThresholdMs)
        {
            return 0;
        }

        return Math.Min(10, (averageLatencyMs - AverageLatencyThresholdMs) / 200);
    }

    private static double CalculateP95LatencyPenalty(double p95LatencyMs)
    {
        if (p95LatencyMs <= P95LatencyThresholdMs)
        {
            return 0;
        }

        return Math.Min(15, (p95LatencyMs - P95LatencyThresholdMs) / 300);
    }

    private static double CalculateRecentFailurePenalty(EndpointHealthScoreRequestDto request, DateTime calculatedAtUtc)
    {
        if (!request.LastFailedDeliveryAtUtc.HasValue)
        {
            return 0;
        }

        var failureAge = calculatedAtUtc - request.LastFailedDeliveryAtUtc.Value;
        if (failureAge < TimeSpan.Zero)
        {
            return 0;
        }

        if (failureAge <= TimeSpan.FromHours(1))
        {
            return 10;
        }

        return failureAge <= TimeSpan.FromHours(24) ? 5 : 0;
    }

    private static EndpointHealthStatus MapHealthStatus(int healthScore)
    {
        return healthScore switch
        {
            >= 90 => EndpointHealthStatus.Healthy,
            >= 70 => EndpointHealthStatus.Degraded,
            >= 40 => EndpointHealthStatus.Unhealthy,
            _ => EndpointHealthStatus.Critical
        };
    }

    private static AiRiskLevel MapRiskLevel(EndpointHealthStatus healthStatus)
    {
        return healthStatus switch
        {
            EndpointHealthStatus.Healthy => AiRiskLevel.Low,
            EndpointHealthStatus.Degraded => AiRiskLevel.Medium,
            EndpointHealthStatus.Unhealthy => AiRiskLevel.High,
            EndpointHealthStatus.Critical => AiRiskLevel.Critical,
            _ => AiRiskLevel.Unknown
        };
    }

    private static string BuildSummary(EndpointHealthStatus healthStatus, EndpointHealthScoreRequestDto request)
    {
        if (healthStatus == EndpointHealthStatus.Healthy)
        {
            return "Endpoint is healthy with reliable recent deliveries.";
        }

        var reasons = new List<string>();
        if (request.FailedDeliveries > 0)
        {
            reasons.Add("delivery failures");
        }

        if (request.TimeoutCount > 0)
        {
            reasons.Add("timeouts");
        }

        if (request.RateLimitCount > 0)
        {
            reasons.Add("rate limiting");
        }

        if (request.ServerErrorCount > 0)
        {
            reasons.Add("server errors");
        }

        if (request.ClientErrorCount > 0)
        {
            reasons.Add("client errors");
        }

        if (request.AverageLatencyMs > AverageLatencyThresholdMs || request.P95LatencyMs > P95LatencyThresholdMs)
        {
            reasons.Add("increased latency");
        }

        if (request.DeadLetterCount > 0)
        {
            reasons.Add("dead-letter records");
        }

        var reasonText = reasons.Count == 0 ? "available delivery signals" : string.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase));
        return $"Endpoint is {healthStatus.ToString().ToLowerInvariant()} due to {reasonText}.";
    }

    private static string BuildRecommendation(EndpointHealthScoreRequestDto request)
    {
        var recommendations = new List<string>();

        if (request.RateLimitCount > 0 || request.LastFailureStatusCode == 429)
        {
            recommendations.Add("Use exponential backoff and reduce delivery concurrency for HTTP 429 rate limit responses");
        }

        if (request.TimeoutCount > 0)
        {
            recommendations.Add("Increase timeout settings or check receiver availability for timeout failures");
        }

        if (request.ServerErrorCount > 0 || request.LastFailureStatusCode is >= 500 and <= 599)
        {
            recommendations.Add("Retry with backoff and monitor receiver health for 5xx failures");
        }

        if (request.ClientErrorCount > 0 || request.LastFailureStatusCode is >= 400 and <= 499 and not 429)
        {
            recommendations.Add("Manually review endpoint configuration, authentication, and payload compatibility for 4xx failures");
        }

        if (request.AverageLatencyMs > AverageLatencyThresholdMs || request.P95LatencyMs > P95LatencyThresholdMs)
        {
            recommendations.Add("Investigate receiver performance because endpoint latency is elevated");
        }

        if (request.DeadLetterCount > 0)
        {
            recommendations.Add("Manually review dead-letter records before replaying deliveries");
        }

        return recommendations.Count == 0
            ? "Continue monitoring the endpoint and keep standard retry policy settings."
            : string.Join(". ", recommendations) + ".";
    }

    private static EndpointHealthScoreResponseDto CreateResponse(
        EndpointHealthScoreRequestDto request,
        int healthScore,
        EndpointHealthStatus healthStatus,
        DateTime calculatedAtUtc,
        string summary,
        string recommendation)
    {
        return new EndpointHealthScoreResponseDto
        {
            EndpointId = request.EndpointId,
            SubscriptionId = request.SubscriptionId,
            CustomerId = request.CustomerId,
            TargetUrl = request.TargetUrl,
            Environment = request.Environment,
            HealthScore = healthScore,
            HealthStatus = healthStatus,
            RiskLevel = MapRiskLevel(healthStatus),
            Summary = summary,
            Recommendation = recommendation,
            CalculatedAtUtc = calculatedAtUtc
        };
    }

    private static void ValidateRequest(EndpointHealthScoreRequestDto request)
    {
        ValidateNonNegative(request.TotalDeliveries, nameof(request.TotalDeliveries));
        ValidateNonNegative(request.SuccessfulDeliveries, nameof(request.SuccessfulDeliveries));
        ValidateNonNegative(request.FailedDeliveries, nameof(request.FailedDeliveries));
        ValidateNonNegative(request.TimeoutCount, nameof(request.TimeoutCount));
        ValidateNonNegative(request.RateLimitCount, nameof(request.RateLimitCount));
        ValidateNonNegative(request.ClientErrorCount, nameof(request.ClientErrorCount));
        ValidateNonNegative(request.ServerErrorCount, nameof(request.ServerErrorCount));
        ValidateNonNegative(request.RetryCount, nameof(request.RetryCount));
        ValidateNonNegative(request.DeadLetterCount, nameof(request.DeadLetterCount));

        if (request.TotalDeliveries > 0 && request.SuccessfulDeliveries + request.FailedDeliveries > request.TotalDeliveries)
        {
            throw new ArgumentException(
                "SuccessfulDeliveries plus FailedDeliveries must not exceed TotalDeliveries when TotalDeliveries is greater than zero.",
                nameof(request));
        }

        if (request.AverageLatencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.AverageLatencyMs), "AverageLatencyMs must be greater than or equal to zero.");
        }

        if (request.P95LatencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.P95LatencyMs), "P95LatencyMs must be greater than or equal to zero.");
        }

        ValidateUtc(request.EvaluationWindowFromUtc, nameof(request.EvaluationWindowFromUtc));
        ValidateUtc(request.EvaluationWindowToUtc, nameof(request.EvaluationWindowToUtc));
        ValidateOptionalUtc(request.LastSuccessfulDeliveryAtUtc, nameof(request.LastSuccessfulDeliveryAtUtc));
        ValidateOptionalUtc(request.LastFailedDeliveryAtUtc, nameof(request.LastFailedDeliveryAtUtc));

        if (request.EvaluationWindowToUtc <= request.EvaluationWindowFromUtc)
        {
            throw new ArgumentException("EvaluationWindowToUtc must be greater than EvaluationWindowFromUtc.", nameof(request));
        }
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be greater than or equal to zero.");
        }
    }

    private static void ValidateOptionalUtc(DateTime? value, string parameterName)
    {
        if (value.HasValue)
        {
            ValidateUtc(value.Value, parameterName);
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"{parameterName} must be a UTC DateTime.", parameterName);
        }
    }
}
