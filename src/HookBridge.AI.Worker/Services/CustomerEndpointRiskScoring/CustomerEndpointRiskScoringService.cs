using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;

/// <summary>
/// Pure, deterministic customer endpoint risk scorer. It does not call Kafka, MongoDB, Ollama, or any external API.
/// </summary>
public sealed class CustomerEndpointRiskScoringService : ICustomerEndpointRiskScoringService
{
    private const double AverageLatencyThresholdMs = 800;
    private const double P95LatencyThresholdMs = 2_000;

    public CustomerEndpointRiskScoreResponseDto CalculateRiskScore(CustomerEndpointRiskScoreRequestDto request, DateTime calculatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(calculatedAtUtc, nameof(calculatedAtUtc));
        ValidateRequest(request);

        if (request.TotalDeliveries == 0)
        {
            return CreateResponse(
                request,
                0,
                AiRiskLevel.Unknown,
                EndpointHealthStatus.Unknown,
                [],
                calculatedAtUtc,
                "Customer endpoint risk is unknown because there are no deliveries in the evaluation window.",
                "Collect delivery data before making customer endpoint risk decisions.");
        }

        var factors = new List<CustomerEndpointRiskFactorDto>();
        AddFailureRateFactor(request, factors);
        AddCountFactor(factors, "RetryBehavior", Math.Min(10, request.RetryCount / 10), request.RetryCount > 0, "Medium", "Endpoint required repeated delivery retries.", "Review retry policy effectiveness and receiver stability.");
        AddCountFactor(factors, "MaxRetryReached", 10, request.MaxRetryCount > 0 && request.RetryCount >= request.MaxRetryCount, "High", "Endpoint reached the configured maximum retry count.", "Review persistent failures and consider dead-letter handling before additional retries.");
        AddCountFactor(factors, "DeadLetterRecords", Math.Min(15, request.DeadLetterCount * 3), request.DeadLetterCount > 0, "High", "Endpoint has deliveries in dead-letter storage.", "Review dead-letter records before replay.");
        AddCountFactor(factors, "TimeoutFailures", Math.Min(10, request.TimeoutCount * 2), request.TimeoutCount > 0, "Medium", "Endpoint experienced timeout failures.", "Check receiver availability and timeout settings.");
        AddCountFactor(factors, "RateLimitFailures", request.RateLimitCount > 0 ? Math.Min(15, request.RateLimitCount * 3) : 5, request.RateLimitCount > 0 || request.LastStatusCode == 429, "High", "Endpoint returned HTTP 429 rate limit failures.", "Use exponential backoff and reduce delivery concurrency.");
        AddCountFactor(factors, "ServerErrors", request.ServerErrorCount > 0 ? Math.Min(15, request.ServerErrorCount * 2) : 5, request.ServerErrorCount > 0 || request.LastStatusCode is >= 500 and <= 599, "High", "Endpoint returned 5xx server failures.", "Retry with backoff and add receiver-side monitoring.");
        AddCountFactor(factors, "ClientErrors", request.ClientErrorCount > 0 ? Math.Min(10, request.ClientErrorCount * 2) : 4, request.ClientErrorCount > 0 || request.LastStatusCode is >= 400 and <= 499 and not 401 and not 403 and not 429, "Medium", "Endpoint returned 4xx client failures.", "Review payload shape, endpoint URL, and webhook contract.");
        AddCountFactor(factors, "AuthenticationFailures", request.AuthenticationFailureCount > 0 ? Math.Min(12, request.AuthenticationFailureCount * 4) : 6, request.AuthenticationFailureCount > 0 || request.LastStatusCode is 401 or 403, "High", "Endpoint returned authentication or authorization failures.", "Review authentication credentials and endpoint authorization settings.");
        AddCountFactor(factors, "SignatureValidationFailures", Math.Min(12, request.SignatureValidationFailureCount * 6), request.SignatureValidationFailureCount > 0, "High", "Endpoint reported signature validation failures.", "Check signing secret and timestamp tolerance.");
        AddCountFactor(factors, "SuspiciousPayloads", Math.Min(15, request.SuspiciousPayloadCount * 8), request.SuspiciousPayloadCount > 0, "Critical", "Endpoint received suspicious payload indicators.", "Perform a manual security review before replaying or trusting affected payloads.");
        AddLatencyFactor(request, factors);
        AddRecentFailureFactor(request, calculatedAtUtc, factors);

        var score = (int)Math.Clamp(factors.Sum(factor => factor.ScoreImpact), 0, 100);
        var riskLevel = MapRiskLevel(score);
        var healthStatus = MapHealthStatus(riskLevel);

        return CreateResponse(
            request,
            score,
            riskLevel,
            healthStatus,
            factors,
            calculatedAtUtc,
            BuildSummary(riskLevel, factors),
            BuildRecommendation(factors));
    }

    private static void AddFailureRateFactor(CustomerEndpointRiskScoreRequestDto request, List<CustomerEndpointRiskFactorDto> factors)
    {
        var failureRate = (double)request.FailedDeliveries / request.TotalDeliveries;
        var impact = failureRate switch
        {
            >= 0.50 => 30,
            >= 0.25 => 22,
            >= 0.10 => 15,
            >= 0.05 => 8,
            > 0 => 3,
            _ => 0
        };

        AddCountFactor(
            factors,
            "HighFailureRate",
            impact,
            impact > 0,
            impact >= 22 ? "High" : impact >= 8 ? "Medium" : "Low",
            $"Endpoint failure rate is {failureRate:P1} in the evaluation window.",
            "Investigate repeated delivery failures and prioritize receiver-side remediation.");
    }

    private static void AddLatencyFactor(CustomerEndpointRiskScoreRequestDto request, List<CustomerEndpointRiskFactorDto> factors)
    {
        if (request.AverageLatencyMs > AverageLatencyThresholdMs)
        {
            AddCountFactor(factors, "HighAverageLatency", Math.Min(8, (int)Math.Ceiling((request.AverageLatencyMs - AverageLatencyThresholdMs) / 200)), true, "Medium", "Endpoint average latency is elevated.", "Investigate receiver performance and network latency.");
        }

        if (request.P95LatencyMs > P95LatencyThresholdMs)
        {
            AddCountFactor(factors, "HighP95Latency", Math.Min(12, (int)Math.Ceiling((request.P95LatencyMs - P95LatencyThresholdMs) / 300)), true, "High", "Endpoint P95 latency is elevated.", "Investigate tail latency and receiver capacity.");
        }
    }

    private static void AddRecentFailureFactor(CustomerEndpointRiskScoreRequestDto request, DateTime calculatedAtUtc, List<CustomerEndpointRiskFactorDto> factors)
    {
        if (!request.LastFailedDeliveryAtUtc.HasValue)
        {
            return;
        }

        var age = calculatedAtUtc - request.LastFailedDeliveryAtUtc.Value;
        if (age < TimeSpan.Zero)
        {
            return;
        }

        var impact = age <= TimeSpan.FromHours(1) ? 8 : age <= TimeSpan.FromHours(24) ? 4 : 0;
        AddCountFactor(factors, "RecentFailure", impact, impact > 0, "Medium", "Endpoint had a recent delivery failure.", "Prioritize investigation because the failure pattern is recent.");
    }

    private static void AddCountFactor(List<CustomerEndpointRiskFactorDto> factors, string name, int impact, bool condition, string severity, string description, string recommendation)
    {
        if (!condition || impact <= 0)
        {
            return;
        }

        factors.Add(new CustomerEndpointRiskFactorDto
        {
            FactorName = name,
            Severity = severity,
            ScoreImpact = impact,
            Description = description,
            Recommendation = recommendation
        });
    }

    public static AiRiskLevel MapRiskLevel(int riskScore)
        => riskScore switch
        {
            < 0 => AiRiskLevel.Unknown,
            <= 20 => AiRiskLevel.Low,
            <= 50 => AiRiskLevel.Medium,
            <= 80 => AiRiskLevel.High,
            <= 100 => AiRiskLevel.Critical,
            _ => AiRiskLevel.Critical
        };

    public static EndpointHealthStatus MapHealthStatus(AiRiskLevel riskLevel)
        => riskLevel switch
        {
            AiRiskLevel.Low => EndpointHealthStatus.Healthy,
            AiRiskLevel.Medium => EndpointHealthStatus.Degraded,
            AiRiskLevel.High => EndpointHealthStatus.Unhealthy,
            AiRiskLevel.Critical => EndpointHealthStatus.Critical,
            _ => EndpointHealthStatus.Unknown
        };

    private static string BuildSummary(AiRiskLevel riskLevel, IReadOnlyCollection<CustomerEndpointRiskFactorDto> factors)
    {
        if (riskLevel == AiRiskLevel.Low && factors.Count == 0)
        {
            return "Customer endpoint has low risk with reliable recent deliveries.";
        }

        var factorNames = factors.Take(4).Select(factor => factor.FactorName).ToArray();
        return $"Customer endpoint has {riskLevel.ToString().ToLowerInvariant()} risk due to {string.Join(", ", factorNames)}.";
    }

    private static string BuildRecommendation(IReadOnlyCollection<CustomerEndpointRiskFactorDto> factors)
    {
        if (factors.Count == 0)
        {
            return "Continue monitoring the endpoint using standard delivery and retry policies.";
        }

        return string.Join(" ", factors.Select(factor => factor.Recommendation).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static CustomerEndpointRiskScoreResponseDto CreateResponse(CustomerEndpointRiskScoreRequestDto request, int score, AiRiskLevel riskLevel, EndpointHealthStatus healthStatus, List<CustomerEndpointRiskFactorDto> factors, DateTime calculatedAtUtc, string summary, string recommendation)
        => new()
        {
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            TargetUrl = request.TargetUrl,
            Environment = request.Environment,
            RiskScore = score,
            RiskLevel = riskLevel,
            ConfidenceScore = factors.Count == 0 ? 0.60 : Math.Min(0.90, 0.70 + factors.Count * 0.03),
            ConfidenceLevel = factors.Count >= 7 ? AiConfidenceLevel.VeryHigh : AiConfidenceLevel.High,
            ConfidenceExplanation = factors.Count == 0 ? "Endpoint risk score used limited deterministic evidence." : "Endpoint risk score used multiple deterministic risk factors.",
            HealthStatus = healthStatus,
            Summary = summary,
            Recommendation = recommendation,
            RiskFactors = factors,
            CalculatedAtUtc = calculatedAtUtc
        };

    private static void ValidateRequest(CustomerEndpointRiskScoreRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("CustomerId is required.", nameof(request));
        }

        if (request.SubscriptionId is not null && string.IsNullOrWhiteSpace(request.SubscriptionId))
        {
            throw new ArgumentException("SubscriptionId is required when available.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("TargetUrl must be a valid absolute HTTP or HTTPS URL when provided.", nameof(request));
            }
        }

        ValidateNonNegative(request.TotalDeliveries, nameof(request.TotalDeliveries));
        ValidateNonNegative(request.SuccessfulDeliveries, nameof(request.SuccessfulDeliveries));
        ValidateNonNegative(request.FailedDeliveries, nameof(request.FailedDeliveries));
        ValidateNonNegative(request.RetryCount, nameof(request.RetryCount));
        ValidateNonNegative(request.MaxRetryCount, nameof(request.MaxRetryCount));
        ValidateNonNegative(request.DeadLetterCount, nameof(request.DeadLetterCount));
        ValidateNonNegative(request.TimeoutCount, nameof(request.TimeoutCount));
        ValidateNonNegative(request.RateLimitCount, nameof(request.RateLimitCount));
        ValidateNonNegative(request.ClientErrorCount, nameof(request.ClientErrorCount));
        ValidateNonNegative(request.ServerErrorCount, nameof(request.ServerErrorCount));
        ValidateNonNegative(request.AuthenticationFailureCount, nameof(request.AuthenticationFailureCount));
        ValidateNonNegative(request.SignatureValidationFailureCount, nameof(request.SignatureValidationFailureCount));
        ValidateNonNegative(request.SuspiciousPayloadCount, nameof(request.SuspiciousPayloadCount));

        if (request.AverageLatencyMs < 0) throw new ArgumentOutOfRangeException(nameof(request.AverageLatencyMs), "AverageLatencyMs must be greater than or equal to zero.");
        if (request.P95LatencyMs < 0) throw new ArgumentOutOfRangeException(nameof(request.P95LatencyMs), "P95LatencyMs must be greater than or equal to zero.");
        if (request.TotalDeliveries > 0 && request.SuccessfulDeliveries + request.FailedDeliveries > request.TotalDeliveries) throw new ArgumentException("SuccessfulDeliveries plus FailedDeliveries must not exceed TotalDeliveries when TotalDeliveries is greater than zero.", nameof(request));

        ValidateUtc(request.EvaluationWindowFromUtc, nameof(request.EvaluationWindowFromUtc));
        ValidateUtc(request.EvaluationWindowToUtc, nameof(request.EvaluationWindowToUtc));
        ValidateOptionalUtc(request.LastSuccessfulDeliveryAtUtc, nameof(request.LastSuccessfulDeliveryAtUtc));
        ValidateOptionalUtc(request.LastFailedDeliveryAtUtc, nameof(request.LastFailedDeliveryAtUtc));
        if (request.EvaluationWindowToUtc <= request.EvaluationWindowFromUtc) throw new ArgumentException("EvaluationWindowToUtc must be greater than EvaluationWindowFromUtc.", nameof(request));
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be greater than or equal to zero.");
    }

    private static void ValidateOptionalUtc(DateTime? value, string parameterName)
    {
        if (value.HasValue) ValidateUtc(value.Value, parameterName);
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException($"{parameterName} must be a UTC DateTime.", parameterName);
    }
}
