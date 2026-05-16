using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;

public sealed class WebhookFailureAnomalyDetectionService : IWebhookFailureAnomalyDetectionService
{
    private const int DetectionThreshold = 25;

    public WebhookFailureAnomalyDetectionResponseDto DetectAnomalies(WebhookFailureAnomalyDetectionRequestDto request, DateTime calculatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        calculatedAtUtc = EnsureUtc(calculatedAtUtc, nameof(calculatedAtUtc));

        var current = request.CurrentWindow!;
        var baseline = request.BaselineWindow!;
        var anomalies = new List<WebhookFailureAnomalyDto>();

        AddPercentageAnomaly(anomalies, "FailureRate", FailureRate(current), FailureRate(baseline), 50, 25, "Webhook delivery failure rate increased significantly.", "Review webhook failures, receiver availability, recent deployments, delivery error patterns, and retry strategy.");
        AddPercentageAnomaly(anomalies, nameof(current.RetryCount), current.RetryCount, baseline.RetryCount, 50, 10, "Retry volume increased significantly.", "Review retry pressure and apply exponential backoff where possible.");
        AddPercentageAnomaly(anomalies, nameof(current.DeadLetterCount), current.DeadLetterCount, baseline.DeadLetterCount, 25, 12, "Dead-letter records increased significantly.", "Review DLQ records before replaying events.");
        AddPercentageAnomaly(anomalies, nameof(current.TimeoutCount), current.TimeoutCount, baseline.TimeoutCount, 50, 12, "Timeout failures increased significantly.", "Check receiver availability, network latency, and timeout settings.");
        AddPercentageAnomaly(anomalies, nameof(current.RateLimitCount), current.RateLimitCount, baseline.RateLimitCount, 50, 15, "HTTP 429 rate-limit failures increased significantly.", "Use exponential backoff and reduce delivery concurrency.");
        AddPercentageAnomaly(anomalies, nameof(current.ServerErrorCount), current.ServerErrorCount, baseline.ServerErrorCount, 50, 12, "HTTP 5xx receiver errors increased significantly.", "Monitor receiver health and retry with backoff.");
        AddPercentageAnomaly(anomalies, nameof(current.ClientErrorCount), current.ClientErrorCount, baseline.ClientErrorCount, 50, 10, "HTTP 4xx client errors increased significantly.", "Review payload contract, endpoint URL, and auth setup.");
        AddPercentageAnomaly(anomalies, nameof(current.AuthenticationFailureCount), current.AuthenticationFailureCount, baseline.AuthenticationFailureCount, 25, 25, "Authentication failures increased significantly.", "Verify credentials, token expiry, and header configuration.");
        AddZeroBaselineAnomaly(anomalies, nameof(current.SignatureValidationFailureCount), current.SignatureValidationFailureCount, baseline.SignatureValidationFailureCount, 12, "Signature validation failures appeared in the current window.", "Verify signing secret and timestamp tolerance.");
        AddZeroBaselineAnomaly(anomalies, nameof(current.SuspiciousPayloadCount), current.SuspiciousPayloadCount, baseline.SuspiciousPayloadCount, 15, "Suspicious payload detections appeared in the current window.", "Require manual security review before replaying or processing affected events.");
        AddPercentageAnomaly(anomalies, nameof(current.AverageLatencyMs), current.AverageLatencyMs, baseline.AverageLatencyMs, 50, 8, "Average webhook latency increased significantly.", "Investigate receiver performance and network latency.");
        AddPercentageAnomaly(anomalies, nameof(current.P95LatencyMs), current.P95LatencyMs, baseline.P95LatencyMs, 50, 10, "P95 webhook latency increased significantly.", "Investigate receiver performance.");

        var score = Math.Clamp(anomalies.Sum(anomaly => anomaly.ScoreImpact), 0, 100);
        var insufficientData = current.TotalDeliveries <= 0 || baseline.TotalDeliveries <= 0;
        var riskLevel = insufficientData ? AiRiskLevel.Unknown : MapRiskLevel(score);

        return new WebhookFailureAnomalyDetectionResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            TargetUrl = request.TargetUrl,
            Environment = request.Environment,
            EventType = request.EventType,
            IsAnomalyDetected = !insufficientData && score >= DetectionThreshold,
            AnomalyScore = score,
            RiskLevel = riskLevel,
            ConfidenceScore = insufficientData ? 0.55 : Math.Min(0.90, 0.70 + anomalies.Count * 0.05),
            ConfidenceLevel = insufficientData ? AiConfidenceLevel.Medium : AiConfidenceLevel.High,
            ConfidenceExplanation = insufficientData ? "Anomaly detection had insufficient baseline or current data." : "Anomaly detection used deterministic metric-window evidence.",
            Summary = BuildSummary(insufficientData, anomalies),
            Recommendation = BuildRecommendation(insufficientData, anomalies),
            DetectedAnomalies = anomalies,
            CalculatedAtUtc = calculatedAtUtc
        };
    }

    public static AiRiskLevel MapRiskLevel(int score) => score switch
    {
        <= 20 => AiRiskLevel.Low,
        <= 50 => AiRiskLevel.Medium,
        <= 80 => AiRiskLevel.High,
        _ => AiRiskLevel.Critical
    };

    private static void AddPercentageAnomaly(List<WebhookFailureAnomalyDto> anomalies, string metricName, double currentValue, double baselineValue, double threshold, int scoreImpact, string description, string recommendation)
    {
        var percentageIncrease = CalculatePercentageIncrease(currentValue, baselineValue);
        if (percentageIncrease < threshold) return;
        anomalies.Add(CreateAnomaly(metricName, currentValue, baselineValue, percentageIncrease, scoreImpact, description, recommendation));
    }

    private static void AddZeroBaselineAnomaly(List<WebhookFailureAnomalyDto> anomalies, string metricName, double currentValue, double baselineValue, int scoreImpact, string description, string recommendation)
    {
        if (baselineValue != 0 || currentValue < 1) return;
        anomalies.Add(CreateAnomaly(metricName, currentValue, baselineValue, 100, scoreImpact, description, recommendation));
    }

    private static WebhookFailureAnomalyDto CreateAnomaly(string metricName, double currentValue, double baselineValue, double percentageIncrease, int scoreImpact, string description, string recommendation)
        => new()
        {
            MetricName = metricName,
            CurrentValue = currentValue,
            BaselineValue = baselineValue,
            PercentageIncrease = Math.Round(percentageIncrease, 2),
            ScoreImpact = scoreImpact,
            Severity = scoreImpact >= 15 ? "High" : scoreImpact >= 10 ? "Medium" : "Low",
            Description = description,
            Recommendation = recommendation
        };

    private static double CalculatePercentageIncrease(double currentValue, double baselineValue)
    {
        if (baselineValue <= 0) return currentValue > 0 ? 100 : 0;
        return ((currentValue - baselineValue) / baselineValue) * 100;
    }

    private static double FailureRate(WebhookFailureMetricWindowDto window)
        => window.TotalDeliveries <= 0 ? 0 : (double)window.FailedDeliveries / window.TotalDeliveries * 100;

    private static string BuildSummary(bool insufficientData, IReadOnlyCollection<WebhookFailureAnomalyDto> anomalies)
    {
        if (insufficientData) return "Insufficient current or baseline delivery data is available for deterministic anomaly detection.";
        if (anomalies.Count == 0) return "No webhook failure spike was detected; current metrics are within the configured baseline thresholds.";
        var metricNames = string.Join(", ", anomalies.Select(anomaly => anomaly.MetricName));
        return $"A webhook failure anomaly was detected. The following metrics increased compared to the baseline window: {metricNames}.";
    }

    private static string BuildRecommendation(bool insufficientData, IReadOnlyCollection<WebhookFailureAnomalyDto> anomalies)
    {
        if (insufficientData) return "Collect non-zero current and baseline delivery windows before acting on anomaly scores.";
        if (anomalies.Count == 0) return "Continue monitoring delivery health and keep baseline windows current.";
        return string.Join(" ", anomalies.Select(anomaly => anomaly.Recommendation).Distinct());
    }

    private static void Validate(WebhookFailureAnomalyDetectionRequestDto request)
    {
        if (request.CurrentWindow is null) throw new ArgumentException("CurrentWindow is required.", nameof(request));
        if (request.BaselineWindow is null) throw new ArgumentException("BaselineWindow is required.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.TargetUrl))
        {
            if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri)) throw new ArgumentException("TargetUrl must be a valid absolute URL.", nameof(request));
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("TargetUrl must be an HTTP or HTTPS URL.", nameof(request));
        }
        EnsureUtc(request.CreatedAtUtc, nameof(request.CreatedAtUtc));
        ValidateWindow(request.CurrentWindow, nameof(request.CurrentWindow));
        ValidateWindow(request.BaselineWindow, nameof(request.BaselineWindow));
    }

    private static void ValidateWindow(WebhookFailureMetricWindowDto window, string name)
    {
        EnsureUtc(window.WindowStartUtc, $"{name}.WindowStartUtc");
        EnsureUtc(window.WindowEndUtc, $"{name}.WindowEndUtc");
        if (window.WindowEndUtc <= window.WindowStartUtc) throw new ArgumentException($"{name}.WindowEndUtc must be greater than {name}.WindowStartUtc.", name);
        if (window.TotalDeliveries < 0 || window.SuccessfulDeliveries < 0 || window.FailedDeliveries < 0 || window.RetryCount < 0 || window.DeadLetterCount < 0 || window.TimeoutCount < 0 || window.RateLimitCount < 0 || window.ClientErrorCount < 0 || window.ServerErrorCount < 0 || window.AuthenticationFailureCount < 0 || window.SignatureValidationFailureCount < 0 || window.SuspiciousPayloadCount < 0 || window.AverageLatencyMs < 0 || window.P95LatencyMs < 0)
        {
            throw new ArgumentException($"{name} metric values must be greater than or equal to zero.", name);
        }
    }

    private static DateTime EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException($"{name} must be a UTC DateTime.", name);
        return value;
    }
}
