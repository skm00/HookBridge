using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;
using HookBridge.AI.Worker.Services.Confidence;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.ObservabilityAgent;
using HookBridge.AI.Worker.Services.PayloadSchemaDetection;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.RetryAgent;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using HookBridge.AI.Worker.Services.SecurityAgent;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using HookBridge.AI.Worker.Services.TransformationAgent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.Orchestration;

public sealed class AiAgentOrchestrator : IAiAgentOrchestrator
{
    private readonly IAiRetryRecommendationService _retryService;
    private readonly IRetryAgent _retryAgent;
    private readonly ISecurityAgent _securityAgent;
    private readonly IWebhookDuplicateReplayDetectionService _duplicateReplayService;
    private readonly IPayloadSchemaDetectionAgent _payloadSchemaAgent;
    private readonly ICustomerEndpointRiskScoringService _endpointRiskService;
    private readonly IWebhookFailureAnomalyDetectionService _anomalyService;
    private readonly IAiLogSummarizationService _logSummarizationService;
    private readonly ITransformationAgent _transformationAgent;
    private readonly IObservabilityAgent _observabilityAgent;
    private readonly IHumanApprovalWorkflowService _approvalWorkflowService;
    private readonly IAiConfidenceScoreService _confidenceScoreService;
    private readonly ILogger<AiAgentOrchestrator> _logger;
    private readonly AiAgentOrchestrationOptions _options;

    public AiAgentOrchestrator(
        IAiRetryRecommendationService retryService,
        IRetryAgent retryAgent,
        ISecurityAgent securityAgent,
        IWebhookDuplicateReplayDetectionService duplicateReplayService,
        IPayloadSchemaDetectionAgent payloadSchemaAgent,
        ICustomerEndpointRiskScoringService endpointRiskService,
        IWebhookFailureAnomalyDetectionService anomalyService,
        IAiLogSummarizationService logSummarizationService,
        ITransformationAgent transformationAgent,
        IObservabilityAgent observabilityAgent,
        IHumanApprovalWorkflowService approvalWorkflowService,
        IAiConfidenceScoreService confidenceScoreService,
        IOptions<AiAgentOrchestrationOptions> options,
        ILogger<AiAgentOrchestrator> logger)
    {
        _retryService = retryService;
        _retryAgent = retryAgent;
        _securityAgent = securityAgent;
        _duplicateReplayService = duplicateReplayService;
        _payloadSchemaAgent = payloadSchemaAgent;
        _endpointRiskService = endpointRiskService;
        _anomalyService = anomalyService;
        _logSummarizationService = logSummarizationService;
        _transformationAgent = transformationAgent;
        _observabilityAgent = observabilityAgent;
        _approvalWorkflowService = approvalWorkflowService;
        _confidenceScoreService = confidenceScoreService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiAgentOrchestrationResponseDto> OrchestrateAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        _logger.LogInformation("AI agent orchestration started. EventId: {EventId}, CorrelationId: {CorrelationId}, Mode: {Mode}", request.EventId, request.CorrelationId, _options.Mode);

        var agentFactories = BuildAgentFactories(request);
        var results = _options.Enabled
            ? await ExecuteAgentsAsync(agentFactories, request, cancellationToken)
            : [];

        var overallRisk = CalculateOverallRisk(results);
        var action = DetermineRecommendedAction(request, results, overallRisk);
        var baseRequiresApproval = RequiresApproval(overallRisk) || results.Any(result => result.RequiresApproval);
        var confidenceResult = CalculateOrchestrationConfidence(results, overallRisk);
        var confidence = confidenceResult.ConfidenceScore;
        var requiresApproval = baseRequiresApproval || _confidenceScoreService.RequiresManualReview(confidence, overallRisk);
        var summary = BuildSummary(results, overallRisk, action);

        var response = new AiAgentOrchestrationResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            OverallSummary = summary,
            OverallRiskLevel = overallRisk,
            RecommendedAction = action,
            ConfidenceScore = confidence,
            ConfidenceLevel = confidenceResult.ConfidenceLevel,
            ConfidenceExplanation = confidenceResult.Explanation,
            AgentResults = results,
            RequiresApproval = requiresApproval,
            GeneratedAtUtc = DateTime.UtcNow
        };

        if (response.RequiresApproval)
        {
            var approval = await _approvalWorkflowService.CreateAsync(new HumanApprovalWorkflowCreateRequestDto
            {
                RecommendationId = $"orchestrator:{request.EventId}:{response.GeneratedAtUtc:O}",
                RecommendationType = AiRecommendationType.AnomalyRecommendation,
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                CustomerId = request.CustomerId,
                CustomerIdType = request.CustomerIdType,
                SubscriptionId = request.SubscriptionId,
                EndpointId = request.EndpointId,
                Environment = request.Environment,
                RiskLevel = response.OverallRiskLevel.ToString(),
                SuggestedAction = response.RecommendedAction.ToString(),
                Summary = response.OverallSummary,
                Recommendation = "Review the orchestration result and approve before any production action is applied.",
                ConfidenceScore = response.ConfidenceScore,
                ConfidenceLevel = response.ConfidenceLevel.ToString(),
                ConfidenceExplanation = response.ConfidenceExplanation,
                RequestedBy = "HookBridge.AI.Orchestrator",
                CreatedAtUtc = response.GeneratedAtUtc
            }, cancellationToken);
            response.ApprovalId = approval.ApprovalId;
            _logger.LogInformation("Approval required by agent/orchestrator. EventId: {EventId}, CorrelationId: {CorrelationId}, ApprovalId: {ApprovalId}", request.EventId, request.CorrelationId, response.ApprovalId);
        }

        _logger.LogInformation("AI agent orchestration completed. EventId: {EventId}, CorrelationId: {CorrelationId}, OverallRiskLevel: {OverallRiskLevel}, RecommendedAction: {RecommendedAction}, RequiresApproval: {RequiresApproval}, AgentCount: {AgentCount}", request.EventId, request.CorrelationId, response.OverallRiskLevel, response.RecommendedAction, response.RequiresApproval, response.AgentResults.Count);
        return response;
    }

    private async Task<IReadOnlyList<AiAgentResultDto>> ExecuteAgentsAsync(
        IReadOnlyList<(AiAgentName Name, Func<CancellationToken, Task<AiAgentResultDto>> Execute)> agentFactories,
        AiAgentOrchestrationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (_options.Mode == AiOrchestrationMode.Parallel)
        {
            var tasks = agentFactories.Select(agent => ExecuteAgentWithIsolationAsync(agent.Name, agent.Execute, request, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        var results = new List<AiAgentResultDto>();
        foreach (var agent in agentFactories)
        {
            results.Add(await ExecuteAgentWithIsolationAsync(agent.Name, agent.Execute, request, cancellationToken));
        }

        return results;
    }

    private async Task<AiAgentResultDto> ExecuteAgentWithIsolationAsync(
        AiAgentName agentName,
        Func<CancellationToken, Task<AiAgentResultDto>> execute,
        AiAgentOrchestrationRequestDto request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("AI agent started. AgentName: {AgentName}, EventId: {EventId}, CorrelationId: {CorrelationId}", agentName, request.EventId, request.CorrelationId);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, _options.AgentTimeoutSeconds)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var result = await execute(linkedCts.Token);
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            _logger.LogInformation("AI agent completed. AgentName: {AgentName}, EventId: {EventId}, CorrelationId: {CorrelationId}, RiskLevel: {RiskLevel}, ConfidenceScore: {ConfidenceScore}, DurationMs: {DurationMs}", agentName, request.EventId, request.CorrelationId, result.RiskLevel, result.ConfidenceScore, result.DurationMs);
            return result;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "AI agent timeout. AgentName: {AgentName}, EventId: {EventId}, CorrelationId: {CorrelationId}", agentName, request.EventId, request.CorrelationId);
            return Failed(agentName, "Agent timed out.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "AI agent failed. AgentName: {AgentName}, EventId: {EventId}, CorrelationId: {CorrelationId}", agentName, request.EventId, request.CorrelationId);
            return Failed(agentName, ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private IReadOnlyList<(AiAgentName Name, Func<CancellationToken, Task<AiAgentResultDto>> Execute)> BuildAgentFactories(AiAgentOrchestrationRequestDto request)
    {
        var agents = new List<(AiAgentName Name, Func<CancellationToken, Task<AiAgentResultDto>> Execute)>();
        if (_options.EnableRetryAgent)
        {
            agents.Add((AiAgentName.RetryRecommendationAgent, ct => RunRetryAgentAsync(request, ct)));
        }
        if (_options.EnableSecurityAgent)
        {
            agents.Add((AiAgentName.SecurityAgent, ct => RunSecurityAgentAsync(request, ct)));
        }
        if (_options.EnableDuplicateReplayAgent)
        {
            agents.Add((AiAgentName.DuplicateReplayDetectionAgent, ct => RunDuplicateReplayAgentAsync(request, ct)));
        }
        if (_options.EnablePayloadSchemaAgent)
        {
            agents.Add((AiAgentName.PayloadSchemaDetectionAgent, ct => RunPayloadSchemaAgentAsync(request, ct)));
        }
        if (_options.EnableEndpointRiskAgent && !string.IsNullOrWhiteSpace(request.CustomerId))
        {
            agents.Add((AiAgentName.EndpointRiskScoringAgent, ct => Task.FromResult(RunEndpointRiskAgent(request))));
        }
        if (_options.EnableAnomalyAgent && !string.IsNullOrWhiteSpace(request.CustomerId))
        {
            agents.Add((AiAgentName.AnomalyDetectionAgent, ct => Task.FromResult(RunAnomalyAgent(request))));
        }
        if (_options.EnableLogSummaryAgent)
        {
            agents.Add((AiAgentName.LogSummarizationAgent, ct => RunLogSummaryAgentAsync(request, ct)));
        }
        if (_options.EnableTransformationAgent)
        {
            agents.Add((AiAgentName.TransformationAgent, ct => RunTransformationAgentAsync(request, ct)));
        }
        if (_options.EnableObservabilityAgent)
        {
            agents.Add((AiAgentName.ObservabilityAgent, ct => RunObservabilityAgentAsync(request, ct)));
        }
        return agents;
    }

    private async Task<AiAgentResultDto> RunRetryAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _retryAgent.AnalyzeAsync(new RetryAgentRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            EventType = request.EventType,
            TargetUrl = request.TargetUrl,
            StatusCode = request.StatusCode,
            FailureReason = request.FailureReason,
            RetryCount = request.RetryCount,
            MaxRetryCount = request.MaxRetryCount,
            FailedAtUtc = request.ReceivedAtUtc,
            PayloadSizeBytes = request.Payload?.ToString()?.Length ?? 0
        }, cancellationToken);

        var riskLevel = Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var parsedRisk) ? parsedRisk : AiRiskLevel.Unknown;
        return Success(AiAgentName.RetryRecommendationAgent, response.Summary, riskLevel, MapRetryAgentSuggestedAction(response.RetryDecision), response.ConfidenceScore, response.Fallback);
    }

    private static string MapRetryAgentSuggestedAction(RetryAgentDecision decision) => decision switch
    {
        RetryAgentDecision.RetryImmediately => SuggestedRetryAction.RetryImmediately.ToString(),
        RetryAgentDecision.RetryWithFixedDelay or RetryAgentDecision.RetryWithExponentialBackoff => SuggestedRetryAction.RetryWithBackoff.ToString(),
        RetryAgentDecision.MoveToDeadLetter => SuggestedRetryAction.MoveToDeadLetter.ToString(),
        RetryAgentDecision.PauseEndpoint => SuggestedRetryAction.PauseEndpoint.ToString(),
        RetryAgentDecision.RequireManualReview => SuggestedRetryAction.RequireManualReview.ToString(),
        _ => SuggestedRetryAction.None.ToString()
    };

    private async Task<AiAgentResultDto> RunSecurityAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _securityAgent.AnalyzeAsync(new SecurityAgentRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            Source = request.Source,
            EventType = request.EventType,
            TargetUrl = request.TargetUrl,
            Headers = request.Headers,
            Payload = request.Payload,
            PayloadSizeBytes = request.Payload?.ToString()?.Length ?? 0,
            ReceivedAtUtc = request.ReceivedAtUtc
        }, cancellationToken);

        return Success(AiAgentName.SecurityAgent, response.Summary, response.RiskLevel, response.SecurityDecision.ToString(), response.ConfidenceScore, response.Fallback);
    }

    private async Task<AiAgentResultDto> RunDuplicateReplayAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _duplicateReplayService.DetectAsync(new WebhookDuplicateReplayDetectionRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            EventType = request.EventType,
            Source = request.Source,
            TargetUrl = request.TargetUrl,
            Headers = request.Headers,
            Payload = request.Payload,
            ReceivedAtUtc = request.ReceivedAtUtc
        }, cancellationToken);

        return Success(AiAgentName.DuplicateReplayDetectionAgent, response.Summary, response.RiskLevel, response.SuggestedAction.ToString(), ScoreToConfidence(response.DetectionScore), false);
    }

    private async Task<AiAgentResultDto> RunPayloadSchemaAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _payloadSchemaAgent.DetectAsync(new PayloadSchemaDetectionRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Source = request.Source,
            EventType = request.EventType,
            CustomerId = request.CustomerId,
            Payload = request.Payload,
            Headers = request.Headers,
            ReceivedAtUtc = request.ReceivedAtUtc
        }, cancellationToken);

        return Success(AiAgentName.PayloadSchemaDetectionAgent, response.Summary, ParseRisk(response.RiskLevel), "GenerateDtoAndValidation", response.ConfidenceScore, response.Fallback?.UsedFallback ?? false);
    }

    private AiAgentResultDto RunEndpointRiskAgent(AiAgentOrchestrationRequestDto request)
    {
        var status = request.StatusCode;
        var response = _endpointRiskService.CalculateRiskScore(new CustomerEndpointRiskScoreRequestDto
        {
            CustomerId = request.CustomerId ?? string.Empty,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            TargetUrl = request.TargetUrl,
            Environment = request.Environment,
            TotalDeliveries = 1,
            SuccessfulDeliveries = status is >= 200 and <= 299 ? 1 : 0,
            FailedDeliveries = status is >= 400 ? 1 : 0,
            RetryCount = request.RetryCount,
            MaxRetryCount = request.MaxRetryCount,
            RateLimitCount = status == 429 ? 1 : 0,
            ClientErrorCount = status is >= 400 and <= 499 ? 1 : 0,
            ServerErrorCount = status is >= 500 ? 1 : 0,
            LastStatusCode = status,
            LastFailureReason = request.FailureReason,
            EvaluationWindowFromUtc = request.ReceivedAtUtc.AddMinutes(-5),
            EvaluationWindowToUtc = request.ReceivedAtUtc
        }, DateTime.UtcNow);

        return Success(AiAgentName.EndpointRiskScoringAgent, response.Summary, response.RiskLevel, response.Recommendation, ScoreToConfidence(response.RiskScore), false);
    }

    private AiAgentResultDto RunAnomalyAgent(AiAgentOrchestrationRequestDto request)
    {
        var failures = request.StatusCode is >= 400 ? 1 : 0;
        var response = _anomalyService.DetectAnomalies(new WebhookFailureAnomalyDetectionRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId ?? string.Empty,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            TargetUrl = request.TargetUrl,
            Environment = request.Environment,
            EventType = request.EventType,
            CurrentWindow = new WebhookFailureMetricWindowDto { WindowStartUtc = request.ReceivedAtUtc.AddMinutes(-5), WindowEndUtc = request.ReceivedAtUtc, FailedDeliveries = failures, TotalDeliveries = 1 },
            BaselineWindow = new WebhookFailureMetricWindowDto { WindowStartUtc = request.ReceivedAtUtc.AddHours(-1), WindowEndUtc = request.ReceivedAtUtc.AddMinutes(-5), FailedDeliveries = 0, TotalDeliveries = 1 }
        }, DateTime.UtcNow);

        return Success(AiAgentName.AnomalyDetectionAgent, response.Summary, response.RiskLevel, response.Recommendation, ScoreToConfidence(response.AnomalyScore), false);
    }

    private async Task<AiAgentResultDto> RunLogSummaryAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var response = await _logSummarizationService.SummarizeAsync(new AiLogSummaryRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Source = request.Source,
            Environment = request.Environment,
            FromUtc = request.ReceivedAtUtc.AddMinutes(-5),
            ToUtc = request.ReceivedAtUtc
        }, cancellationToken);
        return Success(AiAgentName.LogSummarizationAgent, response.Summary, response.RiskLevel, response.Recommendation, response.ConfidenceScore, response.Fallback?.UsedFallback ?? false);
    }

    private async Task<AiAgentResultDto> RunTransformationAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _transformationAgent.AnalyzeAsync(new TransformationAgentRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            EventType = request.EventType,
            Source = request.Source,
            SourcePayload = request.Payload,
            TargetSchema = request.TargetSchema,
            TargetSamplePayload = request.TargetSamplePayload,
            ExistingMappingRules = request.ExistingMappingRules,
            ReceivedAtUtc = request.ReceivedAtUtc
        }, cancellationToken);
        stopwatch.Stop();
        var result = Success(AiAgentName.TransformationAgent, response.Summary, ParseRisk(response.RiskLevel), response.TransformationDecision.ToString(), response.ConfidenceScore, response.Fallback);
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.Decision = response.TransformationDecision.ToString();
        result.RequiresApproval = response.RequiresApproval;
        return result;
    }


    private async Task<AiAgentResultDto> RunObservabilityAgentAsync(AiAgentOrchestrationRequestDto request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var failedDeliveries = request.StatusCode is >= 400 ? 1 : 0;
        var response = await _observabilityAgent.AnalyzeAsync(new ObservabilityAgentRequestDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Environment = request.Environment,
            ServiceName = request.Source ?? "HookBridge.AI.Worker",
            CustomerId = request.CustomerId,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            MongoIsHealthy = true,
            TotalDeliveries = 1,
            FailedDeliveries = failedDeliveries,
            RetryCount = request.RetryCount,
            ErrorLogCount = failedDeliveries,
            EvaluationWindowFromUtc = request.ReceivedAtUtc.AddMinutes(-5),
            EvaluationWindowToUtc = request.ReceivedAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
        stopwatch.Stop();
        var result = Success(AiAgentName.ObservabilityAgent, response.Summary, response.RiskLevel, response.SuggestedActions.FirstOrDefault().ToString(), response.ConfidenceScore, response.Fallback);
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.Decision = response.ObservabilityStatus.ToString();
        result.RequiresApproval = response.RequiresApproval;
        return result;
    }

    public static AiRiskLevel CalculateOverallRisk(IReadOnlyList<AiAgentResultDto> results)
    {
        var successfulRisks = results.Where(result => result.IsSuccessful).Select(result => result.RiskLevel).ToList();
        if (successfulRisks.Count == 0 || successfulRisks.All(risk => risk == AiRiskLevel.Unknown))
        {
            return AiRiskLevel.Unknown;
        }

        return successfulRisks.Max();
    }

    public static double CalculateConfidence(IReadOnlyList<AiAgentResultDto> results)
    {
        var successful = results.Where(result => result.IsSuccessful).ToList();
        if (successful.Count == 0)
        {
            return 0;
        }

        var average = successful.Average(result => result.ConfidenceScore);
        var failedPenalty = (results.Count - successful.Count) * 0.05;
        var fallbackPenalty = successful.Count(result => result.UsedFallback) * 0.03;
        return Math.Clamp(Math.Round(average - failedPenalty - fallbackPenalty, 4), 0, 1);
    }

    private AiConfidenceScoreResponseDto CalculateOrchestrationConfidence(IReadOnlyList<AiAgentResultDto> results, AiRiskLevel riskLevel)
    {
        var successful = results.Where(result => result.IsSuccessful).ToList();
        var averageConfidence = successful.Count == 0 ? 0 : successful.Average(result => result.ConfidenceScore);
        var calculated = _confidenceScoreService.Calculate(new AiConfidenceScoreRequestDto
        {
            DecisionType = AiDecisionType.OrchestrationDecision,
            RiskLevel = riskLevel,
            UsedFallback = successful.Any(result => result.UsedFallback),
            IsRuleBased = true,
            AgentName = nameof(AiAgentOrchestrator),
            EvidenceCount = successful.Count,
            FailedAgentCount = results.Count - successful.Count,
            TotalAgentCount = results.Count,
            CreatedAtUtc = DateTime.UtcNow
        });

        calculated.ConfidenceScore = Math.Clamp(Math.Round((calculated.ConfidenceScore + CalculateConfidence(results) + averageConfidence) / 3d, 4), 0, 1);
        calculated.ConfidenceLevel = _confidenceScoreService.MapConfidenceLevel(calculated.ConfidenceScore);
        calculated.Explanation = $"Orchestration confidence averaged successful agent confidence ({averageConfidence:0.00}) and penalized failed or fallback agents. {calculated.Explanation}";
        return calculated;
    }

    private AiOrchestrationRecommendedAction DetermineRecommendedAction(AiAgentOrchestrationRequestDto request, IReadOnlyList<AiAgentResultDto> results, AiRiskLevel overallRisk)
    {
        if (results.Any(r => (r.AgentName is AiAgentName.SecurityAgent or AiAgentName.SecurityAnalysisAgent) && r.IsSuccessful && r.RiskLevel == AiRiskLevel.Critical))
        {
            return AiOrchestrationRecommendedAction.Quarantine;
        }

        if (results.Any(r => r.AgentName == AiAgentName.DuplicateReplayDetectionAgent && r.IsSuccessful && r.RiskLevel >= AiRiskLevel.High && (Contains(r.Summary, "replay") || Contains(r.SuggestedAction, "Quarantine") || Contains(r.SuggestedAction, "Reject"))))
        {
            return AiOrchestrationRecommendedAction.Quarantine;
        }

        if (results.Any(r => r.AgentName == AiAgentName.DuplicateReplayDetectionAgent && r.IsSuccessful && Contains(r.SuggestedAction, "IgnoreDuplicate")))
        {
            return AiOrchestrationRecommendedAction.MoveToDeadLetter;
        }

        if (request.MaxRetryCount > 0 && request.RetryCount >= request.MaxRetryCount)
        {
            return AiOrchestrationRecommendedAction.MoveToDeadLetter;
        }

        if (request.StatusCode == StatusCodes.Status429 || results.Any(r => r.AgentName == AiAgentName.RetryRecommendationAgent && r.IsSuccessful && Contains(r.SuggestedAction, "RetryWithBackoff")))
        {
            return AiOrchestrationRecommendedAction.RetryWithBackoff;
        }

        if (overallRisk >= AiRiskLevel.High)
        {
            return AiOrchestrationRecommendedAction.RequireManualReview;
        }

        return overallRisk == AiRiskLevel.Low ? AiOrchestrationRecommendedAction.Allow : AiOrchestrationRecommendedAction.None;
    }

    private bool RequiresApproval(AiRiskLevel riskLevel)
        => (riskLevel == AiRiskLevel.High && _options.RequireApprovalForHighRisk)
           || (riskLevel == AiRiskLevel.Critical && _options.RequireApprovalForCriticalRisk);

    private static string BuildSummary(IReadOnlyList<AiAgentResultDto> results, AiRiskLevel risk, AiOrchestrationRecommendedAction action)
    {
        var successful = results.Where(result => result.IsSuccessful && !string.IsNullOrWhiteSpace(result.Summary)).Take(3).Select(result => result.Summary.Trim()).ToList();
        var prefix = successful.Count == 0 ? "No successful agent result was produced." : string.Join(" ", successful);
        return $"{prefix} Overall risk is {risk}; recommended action is {action}.";
    }

    private static AiAgentResultDto Success(AiAgentName name, string summary, AiRiskLevel riskLevel, string suggestedAction, double confidence, bool usedFallback)
        => new()
        {
            AgentName = name,
            IsSuccessful = true,
            Summary = summary,
            RiskLevel = riskLevel,
            SuggestedAction = suggestedAction,
            ConfidenceScore = Math.Clamp(confidence, 0, 1),
            ConfidenceLevel = confidence switch { < 0.40 => AiConfidenceLevel.Low, < 0.70 => AiConfidenceLevel.Medium, < 0.90 => AiConfidenceLevel.High, <= 1 => AiConfidenceLevel.VeryHigh, _ => AiConfidenceLevel.Unknown },
            ConfidenceExplanation = usedFallback ? "Agent used fallback output, reducing confidence." : "Agent confidence was supplied by deterministic agent scoring.",
            UsedFallback = usedFallback
        };

    private static AiAgentResultDto Failed(AiAgentName name, string errorMessage, long durationMs)
        => new()
        {
            AgentName = name,
            IsSuccessful = false,
            Summary = "Agent failed.",
            RiskLevel = AiRiskLevel.Unknown,
            SuggestedAction = AiOrchestrationRecommendedAction.None.ToString(),
            ConfidenceScore = 0,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };

    private static AiRiskLevel ParseRisk(string? riskLevel)
        => Enum.TryParse<AiRiskLevel>(riskLevel, ignoreCase: true, out var parsed) ? parsed : AiRiskLevel.Unknown;

    private static double ScoreToConfidence(int score) => Math.Clamp(score / 100d, 0, 1);

    private static bool Contains(string? value, string token)
        => value?.Contains(token, StringComparison.OrdinalIgnoreCase) == true;

    private static void ValidateRequest(AiAgentOrchestrationRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = new ValidationContext(request);
        Validator.ValidateObject(request, context, validateAllProperties: true);
    }

    private static class StatusCodes
    {
        public const int Status429 = 429;
    }
}
