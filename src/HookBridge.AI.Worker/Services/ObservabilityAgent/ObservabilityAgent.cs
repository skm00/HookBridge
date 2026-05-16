using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.LogSummaries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.ObservabilityAgent;

public sealed class ObservabilityAgent : IObservabilityAgent
{
    private const string PromptName = "observability-agent";
    private const string PromptVersion = "v1.0.0";
    private readonly ObservabilityAgentOptions _options;
    private readonly IAiLogSummarizationService? _logSummarizationService;
    private readonly ILogger<ObservabilityAgent> _logger;

    public ObservabilityAgent(IOptions<ObservabilityAgentOptions> options, ILogger<ObservabilityAgent> logger, IAiLogSummarizationService? logSummarizationService = null)
    {
        _options = options.Value;
        _logger = logger;
        _logSummarizationService = logSummarizationService;
    }

    public async Task<ObservabilityAgentResponseDto> AnalyzeAsync(ObservabilityAgentRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        var validationResults = request.Validate(new ValidationContext(request)).ToArray();
        if (validationResults.Length > 0)
        {
            _logger.LogWarning("Invalid request. EventId: {EventId}, CorrelationId: {CorrelationId}, ValidationErrorCount: {ValidationErrorCount}", request.EventId, request.CorrelationId, validationResults.Length);
            throw new ValidationException(validationResults[0].ErrorMessage);
        }

        _logger.LogInformation("Observability agent started. EventId: {EventId}, CorrelationId: {CorrelationId}, Environment: {Environment}, ServiceName: {ServiceName}", request.EventId, request.CorrelationId, request.Environment, request.ServiceName);

        var signals = new List<ObservabilitySignalDto>();
        var actions = new HashSet<ObservabilitySuggestedAction> { ObservabilitySuggestedAction.Monitor };
        var status = EvaluateTelemetry(request, signals, actions);
        var riskLevel = MapRiskLevel(status);
        var requiresApproval = status == ObservabilityStatus.Critical && _options.RequireApprovalForCriticalStatus;
        var confidence = Clamp(CalculateConfidence(request, signals, status));
        var generatedAtUtc = DateTime.UtcNow;

        _logger.LogInformation("Telemetry evaluated. EventId: {EventId}, CorrelationId: {CorrelationId}, SignalCount: {SignalCount}", request.EventId, request.CorrelationId, signals.Count);
        _logger.LogInformation("Status calculated. EventId: {EventId}, CorrelationId: {CorrelationId}, ObservabilityStatus: {ObservabilityStatus}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}", request.EventId, request.CorrelationId, status, riskLevel, confidence);

        if (signals.Any(signal => string.Equals(signal.Severity, ObservabilityStatus.Critical.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Critical signal detected. EventId: {EventId}, CorrelationId: {CorrelationId}, CriticalSignalCount: {CriticalSignalCount}", request.EventId, request.CorrelationId, signals.Count(signal => string.Equals(signal.Severity, ObservabilityStatus.Critical.ToString(), StringComparison.OrdinalIgnoreCase)));
        }

        if (requiresApproval)
        {
            actions.Add(ObservabilitySuggestedAction.RequireManualReview);
            actions.Add(ObservabilitySuggestedAction.EscalateToSupport);
            _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, ObservabilityStatus: {ObservabilityStatus}", request.EventId, request.CorrelationId, status);
        }

        var summary = BuildSummary(request, status, signals);
        var recommendation = await BuildRecommendationAsync(request, signals, actions, cancellationToken);

        return new ObservabilityAgentResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Environment = request.Environment,
            ServiceName = request.ServiceName,
            ObservabilityStatus = status,
            RiskLevel = riskLevel,
            Summary = summary,
            Recommendation = recommendation,
            Signals = signals,
            SuggestedActions = actions.Count == 0 ? [ObservabilitySuggestedAction.None] : actions.ToList(),
            ConfidenceScore = confidence,
            RequiresApproval = requiresApproval,
            GeneratedAtUtc = generatedAtUtc,
            Fallback = !_options.Enabled,
            PromptName = PromptName,
            PromptVersion = PromptVersion,
            PromptHash = ComputePromptHash(PromptName, PromptVersion)
        };
    }

    private ObservabilityStatus EvaluateTelemetry(ObservabilityAgentRequestDto request, ICollection<ObservabilitySignalDto> signals, ISet<ObservabilitySuggestedAction> actions)
    {
        if (!_options.Enabled) return ObservabilityStatus.Unknown;

        var status = ObservabilityStatus.Healthy;

        if (!request.MongoIsHealthy)
        {
            AddSignal(signals, "MongoHealth", ObservabilityStatus.Critical, "false", "MongoDB health check is failing.", "Check Mongo connection, server availability, and credentials.");
            actions.Add(ObservabilitySuggestedAction.CheckMongoHealth);
            status = Max(status, ObservabilityStatus.Critical);
        }

        if (request.KafkaConsumerLag > _options.KafkaLagCriticalThreshold)
        {
            AddSignal(signals, "KafkaConsumerLag", ObservabilityStatus.Critical, request.KafkaConsumerLag.ToString(), "Kafka consumer lag is above the critical threshold.", "Check Kafka consumer health and scale workers.");
            actions.Add(ObservabilitySuggestedAction.CheckKafkaLag);
            status = Max(status, ObservabilityStatus.Critical);
        }
        else if (request.KafkaConsumerLag > _options.KafkaLagDegradedThreshold)
        {
            AddSignal(signals, "KafkaConsumerLag", ObservabilityStatus.Degraded, request.KafkaConsumerLag.ToString(), "Kafka consumer lag is above the degraded threshold.", "Check consumer health and scale workers if lag keeps rising.");
            actions.Add(ObservabilitySuggestedAction.CheckKafkaLag);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        if (request.MongoLatencyMs > _options.MongoLatencyUnhealthyThresholdMs)
        {
            AddSignal(signals, "MongoLatencyMs", ObservabilityStatus.Unhealthy, request.MongoLatencyMs.ToString(), "MongoDB latency is above the unhealthy threshold.", "Check indexes, server load, and query patterns.");
            actions.Add(ObservabilitySuggestedAction.CheckMongoHealth);
            status = Max(status, ObservabilityStatus.Unhealthy);
        }
        else if (request.MongoLatencyMs > _options.MongoLatencyDegradedThresholdMs)
        {
            AddSignal(signals, "MongoLatencyMs", ObservabilityStatus.Degraded, request.MongoLatencyMs.ToString(), "MongoDB latency is above the degraded threshold.", "Check indexes, server load, and query patterns.");
            actions.Add(ObservabilitySuggestedAction.CheckMongoHealth);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        var failureRate = CalculateFailureRatePercent(request);
        if (failureRate > _options.FailureRateUnhealthyThresholdPercent)
        {
            AddSignal(signals, "FailureRate", ObservabilityStatus.Unhealthy, $"{failureRate:0.##}%", "Webhook delivery failure rate is above the unhealthy threshold.", "Review endpoint failures and retry strategy.");
            actions.Add(ObservabilitySuggestedAction.ReduceWebhookConcurrency);
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Unhealthy);
        }
        else if (failureRate > _options.FailureRateDegradedThresholdPercent)
        {
            AddSignal(signals, "FailureRate", ObservabilityStatus.Degraded, $"{failureRate:0.##}%", "Webhook delivery failure rate is above the degraded threshold.", "Review endpoint failures and retry strategy.");
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        if (request.DeadLetterCount > 0)
        {
            AddSignal(signals, "DeadLetterCount", ObservabilityStatus.Degraded, request.DeadLetterCount.ToString(), "Dead-letter records exist in the evaluation window.", "Review dead-letter queue records before replay.");
            actions.Add(ObservabilitySuggestedAction.ReviewDeadLetterQueue);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        if (request.AnomalyCount > 0)
        {
            AddSignal(signals, "AnomalyCount", ObservabilityStatus.Degraded, request.AnomalyCount.ToString(), "Anomaly findings exist in the evaluation window.", "Review anomaly details and correlated telemetry.");
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        if (request.SecurityFindingCount >= _options.SecurityFindingUnhealthyThreshold && request.SecurityFindingCount > 0)
        {
            AddSignal(signals, "SecurityFindingCount", ObservabilityStatus.Unhealthy, request.SecurityFindingCount.ToString(), "Security finding volume is above the unhealthy threshold.", "Review security analysis results.");
            actions.Add(ObservabilitySuggestedAction.ReviewSecurityFindings);
            status = Max(status, ObservabilityStatus.Unhealthy);
        }
        else if (request.SecurityFindingCount > 0)
        {
            AddSignal(signals, "SecurityFindingCount", ObservabilityStatus.Degraded, request.SecurityFindingCount.ToString(), "Security findings exist in the evaluation window.", "Review security analysis results.");
            actions.Add(ObservabilitySuggestedAction.ReviewSecurityFindings);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        if (request.ErrorLogCount >= _options.ErrorLogCriticalThreshold && request.ErrorLogCount > 0)
        {
            AddSignal(signals, "ErrorLogCount", ObservabilityStatus.Critical, request.ErrorLogCount.ToString(), "Error log count is above the critical threshold.", "Investigate recent errors using trace and correlation identifiers.");
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Critical);
        }
        else if (request.ErrorLogCount > 0)
        {
            AddSignal(signals, "ErrorLogCount", ObservabilityStatus.Degraded, request.ErrorLogCount.ToString(), "Error logs exist in the evaluation window.", "Investigate recent errors using trace and correlation identifiers.");
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        var retryRate = request.TotalDeliveries > 0 ? request.RetryCount * 100d / request.TotalDeliveries : request.RetryCount > 0 ? 100d : 0d;
        if (request.RetryCount > 0 && retryRate > _options.FailureRateDegradedThresholdPercent)
        {
            AddSignal(signals, "RetryCount", ObservabilityStatus.Degraded, request.RetryCount.ToString(), "Retry volume is elevated compared with delivery volume.", "Review retry strategy and endpoint failure causes.");
            actions.Add(ObservabilitySuggestedAction.InvestigateLogs);
            status = Max(status, ObservabilityStatus.Degraded);
        }

        return status;
    }

    public static AiRiskLevel MapRiskLevel(ObservabilityStatus status) => status switch
    {
        ObservabilityStatus.Healthy => AiRiskLevel.Low,
        ObservabilityStatus.Degraded => AiRiskLevel.Medium,
        ObservabilityStatus.Unhealthy => AiRiskLevel.High,
        ObservabilityStatus.Critical => AiRiskLevel.Critical,
        _ => AiRiskLevel.Unknown
    };

    public static bool ShouldPublishAnomaly(ObservabilityAgentResponseDto response)
        => response.ObservabilityStatus is ObservabilityStatus.Unhealthy or ObservabilityStatus.Critical;

    private async Task<string> BuildRecommendationAsync(ObservabilityAgentRequestDto request, IReadOnlyCollection<ObservabilitySignalDto> signals, IReadOnlySet<ObservabilitySuggestedAction> actions, CancellationToken cancellationToken)
    {
        var recommendations = signals.Select(signal => signal.Recommendation).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
        if (recommendations.Count == 0) recommendations.Add("Continue monitoring operational telemetry.");

        if (_options.EnableAiLogSummary && _logSummarizationService is not null && request.RecentErrors.Count > 0)
        {
            try
            {
                var summary = await _logSummarizationService.SummarizeAsync(new AiLogSummaryRequestDto
                {
                    EventId = request.EventId,
                    CorrelationId = request.CorrelationId,
                    Environment = request.Environment,
                    Source = request.ServiceName,
                    FromUtc = request.EvaluationWindowFromUtc,
                    ToUtc = request.EvaluationWindowToUtc,
                    Logs = request.RecentErrors.Select(error => new AiLogEntryDto
                    {
                        TimestampUtc = error.TimestampUtc,
                        Level = error.Level,
                        Message = error.Message,
                        Exception = error.Exception,
                        TraceId = error.TraceId,
                        SpanId = error.SpanId,
                        ServiceName = error.Source
                    }).ToList()
                }, cancellationToken);
                if (!string.IsNullOrWhiteSpace(summary.Recommendation)) recommendations.Add(summary.Recommendation);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI log summary unavailable for observability agent. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            }
        }

        return string.Join(" ", recommendations.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildSummary(ObservabilityAgentRequestDto request, ObservabilityStatus status, IReadOnlyCollection<ObservabilitySignalDto> signals)
    {
        if (signals.Count == 0) return $"{request.ServiceName ?? "Service"} is healthy for the evaluated window.";
        var topSignals = string.Join(", ", signals.Take(4).Select(signal => signal.SignalName));
        return $"{request.ServiceName ?? "Service"} is {status} due to {topSignals}.";
    }

    private static void AddSignal(ICollection<ObservabilitySignalDto> signals, string name, ObservabilityStatus severity, string value, string description, string recommendation)
        => signals.Add(new ObservabilitySignalDto { SignalName = name, Severity = severity.ToString(), Value = value, Description = description, Recommendation = recommendation });

    private static ObservabilityStatus Max(ObservabilityStatus current, ObservabilityStatus candidate) => candidate > current ? candidate : current;
    private static double CalculateFailureRatePercent(ObservabilityAgentRequestDto request) => request.TotalDeliveries <= 0 ? 0 : request.FailedDeliveries * 100d / request.TotalDeliveries;
    private static double Clamp(double value) => Math.Clamp(Math.Round(value, 4), 0, 1);

    private static double CalculateConfidence(ObservabilityAgentRequestDto request, IReadOnlyCollection<ObservabilitySignalDto> signals, ObservabilityStatus status)
    {
        var confidence = status == ObservabilityStatus.Healthy ? 0.86 : 0.74 + Math.Min(signals.Count, 5) * 0.04;
        if (request.TotalDeliveries == 0 && request.KafkaConsumerLag == 0 && request.ErrorLogCount == 0) confidence -= 0.08;
        return confidence;
    }

    private static string ComputePromptHash(string promptName, string promptVersion)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{promptName}:{promptVersion}:deterministic-rules"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
