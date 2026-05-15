using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.Orchestration;
using HookBridge.AI.Worker.Services.PayloadSchemaDetection;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAgentOrchestratorTests
{
    [Fact]
    public async Task SequentialOrchestrationSuccess_IncludesCoreAgentResults()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { Mode = AiOrchestrationMode.Sequential, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest());

        response.EventId.Should().Be("evt-1");
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.RetryRecommendationAgent);
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.SecurityAnalysisAgent);
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.DuplicateReplayDetectionAgent);
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.PayloadSchemaDetectionAgent);
        response.OverallRiskLevel.Should().Be(AiRiskLevel.Medium);
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.RetryWithBackoff);
    }

    [Fact]
    public async Task ParallelOrchestrationSuccess_ReturnsAllEnabledResults()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { Mode = AiOrchestrationMode.Parallel, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest());

        response.AgentResults.Should().HaveCount(4);
        response.AgentResults.Should().OnlyContain(result => result.IsSuccessful);
    }

    [Fact]
    public async Task OneFailedAgent_DoesNotFailOrchestration()
    {
        var fixture = new Fixture();
        fixture.Security.Setup(agent => agent.AnalyzeAsync(It.IsAny<AiSecurityAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest());

        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.SecurityAnalysisAgent && !result.IsSuccessful && result.ErrorMessage == "boom");
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.RetryRecommendationAgent && result.IsSuccessful);
    }

    [Fact]
    public async Task AgentTimeout_IsReturnedAsFailedAgentResult()
    {
        var fixture = new Fixture();
        fixture.Retry.Setup(service => service.AnalyzeAsync(It.IsAny<WebhookFailureAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .Returns<WebhookFailureAnalysisRequestDto, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new WebhookFailureAnalysisResponseDto();
            });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { AgentTimeoutSeconds = 1, EnableSecurityAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest());

        response.AgentResults.Should().ContainSingle().Which.ErrorMessage.Should().Be("Agent timed out.");
    }

    [Fact]
    public async Task CriticalSecurityResult_SetsOverallRiskCriticalAndRequiresApproval()
    {
        var fixture = new Fixture();
        fixture.Security.Setup(agent => agent.AnalyzeAsync(It.IsAny<AiSecurityAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSecurityAnalysisResponseDto { EventId = "evt-1", RiskLevel = AiRiskLevel.Critical, Summary = "critical", SuggestedAction = AiSecuritySuggestedAction.Quarantine, ConfidenceScore = 0.9 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableRetryAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.OverallRiskLevel.Should().Be(AiRiskLevel.Critical);
        response.RequiresApproval.Should().BeTrue();
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.Quarantine);
    }

    [Fact]
    public async Task LowRisk_DoesNotRequireApprovalByDefault()
    {
        var fixture = new Fixture();
        fixture.Retry.Setup(service => service.AnalyzeAsync(It.IsAny<WebhookFailureAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookFailureAnalysisResponseDto { EventId = "evt-1", RiskLevel = AiRiskLevel.Low, AiSummary = "ok", ConfidenceScore = 0.7 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableSecurityAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.RequiresApproval.Should().BeFalse();
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.Allow);
    }

    [Fact]
    public void ConfidenceCalculation_AveragesSuccessfulResultsAndPenalizesFailuresAndFallbacks()
    {
        var results = new[]
        {
            new AiAgentResultDto { IsSuccessful = true, ConfidenceScore = 0.8 },
            new AiAgentResultDto { IsSuccessful = true, ConfidenceScore = 0.6, UsedFallback = true },
            new AiAgentResultDto { IsSuccessful = false, ConfidenceScore = 0 }
        };

        var confidence = AiAgentOrchestrator.CalculateConfidence(results);

        confidence.Should().Be(0.62);
    }

    [Theory]
    [InlineData(429, 0, 3, AiOrchestrationRecommendedAction.RetryWithBackoff)]
    [InlineData(500, 3, 3, AiOrchestrationRecommendedAction.MoveToDeadLetter)]
    public async Task RecommendedAction_MapsRetryScenarios(int statusCode, int retryCount, int maxRetryCount, AiOrchestrationRecommendedAction expected)
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableSecurityAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode, retryCount, maxRetryCount));

        response.RecommendedAction.Should().Be(expected);
    }

    [Fact]
    public async Task RecommendedAction_MapsReplayDetectedToQuarantine()
    {
        var fixture = new Fixture();
        fixture.DuplicateReplay.Setup(service => service.DetectAsync(It.IsAny<WebhookDuplicateReplayDetectionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDuplicateReplayDetectionResponseDto { EventId = "evt-1", IsReplay = true, RiskLevel = AiRiskLevel.High, SuggestedAction = WebhookDuplicateReplaySuggestedAction.Quarantine, Summary = "Replay detected", DetectionScore = 90 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableRetryAgent = false, EnableSecurityAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.Quarantine);
    }



    [Fact]
    public async Task DisabledOrchestration_ReturnsUnknownWithNoAgentResults()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { Enabled = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.AgentResults.Should().BeEmpty();
        response.OverallRiskLevel.Should().Be(AiRiskLevel.Unknown);
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.None);
        response.ConfidenceScore.Should().Be(0);
    }

    [Fact]
    public async Task EndpointRiskAndAnomalyAgents_RunWhenCustomerDataIsAvailable()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableRetryAgent = false, EnableSecurityAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 500));

        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.EndpointRiskScoringAgent && result.IsSuccessful);
        response.AgentResults.Should().Contain(result => result.AgentName == AiAgentName.AnomalyDetectionAgent && result.IsSuccessful);
    }

    [Fact]
    public async Task UnknownRisk_WhenAllAgentsFailOrReturnUnknown()
    {
        var fixture = new Fixture();
        fixture.Retry.Setup(service => service.AnalyzeAsync(It.IsAny<WebhookFailureAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookFailureAnalysisResponseDto { EventId = "evt-1", RiskLevel = AiRiskLevel.Unknown, ConfidenceScore = 0.5 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableSecurityAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.OverallRiskLevel.Should().Be(AiRiskLevel.Unknown);
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.None);
    }

    [Fact]
    public async Task DuplicateIgnoreAction_MapsToMoveToDeadLetter()
    {
        var fixture = new Fixture();
        fixture.DuplicateReplay.Setup(service => service.DetectAsync(It.IsAny<WebhookDuplicateReplayDetectionRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookDuplicateReplayDetectionResponseDto { EventId = "evt-1", IsDuplicate = true, RiskLevel = AiRiskLevel.Medium, SuggestedAction = WebhookDuplicateReplaySuggestedAction.IgnoreDuplicate, Summary = "Duplicate event id detected", DetectionScore = 70 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableRetryAgent = false, EnableSecurityAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.MoveToDeadLetter);
    }

    [Fact]
    public async Task HighRiskWithoutStrongerAction_RequiresManualReview()
    {
        var fixture = new Fixture();
        fixture.Security.Setup(agent => agent.AnalyzeAsync(It.IsAny<AiSecurityAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSecurityAnalysisResponseDto { EventId = "evt-1", RiskLevel = AiRiskLevel.High, Summary = "high risk", SuggestedAction = AiSecuritySuggestedAction.RequireManualReview, ConfidenceScore = 0.8 });
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions { EnableRetryAgent = false, EnableDuplicateReplayAgent = false, EnablePayloadSchemaAgent = false, EnableEndpointRiskAgent = false, EnableAnomalyAgent = false });

        var response = await orchestrator.OrchestrateAsync(CreateRequest(statusCode: 200));

        response.OverallRiskLevel.Should().Be(AiRiskLevel.High);
        response.RequiresApproval.Should().BeTrue();
        response.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.RequireManualReview);
    }

    [Fact]
    public async Task EmptyEventId_ThrowsValidationException()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions());
        var request = CreateRequest();
        request.EventId = string.Empty;

        var act = async () => await orchestrator.OrchestrateAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task InvalidTargetUrl_ThrowsValidationException()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions());
        var request = CreateRequest();
        request.TargetUrl = "not-a-url";

        var act = async () => await orchestrator.OrchestrateAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task NonUtcReceivedAt_ThrowsValidationException()
    {
        var fixture = new Fixture();
        var orchestrator = fixture.Create(new AiAgentOrchestrationOptions());
        var request = CreateRequest();
        request.ReceivedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);

        var act = async () => await orchestrator.OrchestrateAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public void ConfidenceCalculation_ReturnsZeroWhenNoSuccessfulAgents()
    {
        var results = new[]
        {
            new AiAgentResultDto { IsSuccessful = false, ConfidenceScore = 0 }
        };

        var confidence = AiAgentOrchestrator.CalculateConfidence(results);

        confidence.Should().Be(0);
    }

    private static AiAgentOrchestrationRequestDto CreateRequest(int statusCode = 429, int retryCount = 1, int maxRetryCount = 3) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "Tenant",
        SubscriptionId = "sub-1",
        EndpointId = "end-1",
        Environment = "Test",
        EventType = "invoice.created",
        Source = "stripe",
        TargetUrl = "https://example.test/webhooks",
        StatusCode = statusCode,
        FailureReason = "rate limited",
        Headers = new Dictionary<string, string> { ["content-type"] = "application/json" },
        Payload = new { id = "evt-1" },
        RetryCount = retryCount,
        MaxRetryCount = maxRetryCount,
        ReceivedAtUtc = DateTime.UtcNow
    };

    private sealed class Fixture
    {
        public Mock<IAiRetryRecommendationService> Retry { get; } = new();
        public Mock<IAiSecurityAnalysisAgent> Security { get; } = new();
        public Mock<IWebhookDuplicateReplayDetectionService> DuplicateReplay { get; } = new();
        public Mock<IPayloadSchemaDetectionAgent> PayloadSchema { get; } = new();
        public Mock<ICustomerEndpointRiskScoringService> EndpointRisk { get; } = new();
        public Mock<IWebhookFailureAnomalyDetectionService> Anomaly { get; } = new();
        public Mock<IAiLogSummarizationService> LogSummary { get; } = new();
        public Mock<IWebhookTransformationRecommendationAgent> Transformation { get; } = new();

        public Fixture()
        {
            Retry.Setup(service => service.AnalyzeAsync(It.IsAny<WebhookFailureAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookFailureAnalysisResponseDto { EventId = "evt-1", AiSummary = "HTTP 429 indicates rate limiting.", RiskLevel = AiRiskLevel.Medium, SuggestedRetryAction = SuggestedRetryAction.RetryWithBackoff, ConfidenceScore = 0.8 });
            Security.Setup(agent => agent.AnalyzeAsync(It.IsAny<AiSecurityAnalysisRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AiSecurityAnalysisResponseDto { EventId = "evt-1", Summary = "No suspicious payload pattern detected.", RiskLevel = AiRiskLevel.Low, SuggestedAction = AiSecuritySuggestedAction.Allow, ConfidenceScore = 0.7 });
            DuplicateReplay.Setup(service => service.DetectAsync(It.IsAny<WebhookDuplicateReplayDetectionRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebhookDuplicateReplayDetectionResponseDto { EventId = "evt-1", Summary = "No duplicate or replay detected.", RiskLevel = AiRiskLevel.Low, SuggestedAction = WebhookDuplicateReplaySuggestedAction.Allow, DetectionScore = 75 });
            PayloadSchema.Setup(agent => agent.DetectAsync(It.IsAny<PayloadSchemaDetectionRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayloadSchemaDetectionResponseDto { EventId = "evt-1", Summary = "Payload schema detected.", RiskLevel = "Low", ConfidenceScore = 0.6 });
            EndpointRisk.Setup(service => service.CalculateRiskScore(It.IsAny<CustomerEndpointRiskScoreRequestDto>(), It.IsAny<DateTime>()))
                .Returns(new CustomerEndpointRiskScoreResponseDto { CustomerId = "cust-1", RiskLevel = AiRiskLevel.Low, Summary = "Endpoint risk is low.", Recommendation = "Monitor", RiskScore = 40 });
            Anomaly.Setup(service => service.DetectAnomalies(It.IsAny<WebhookFailureAnomalyDetectionRequestDto>(), It.IsAny<DateTime>()))
                .Returns(new WebhookFailureAnomalyDetectionResponseDto { CustomerId = "cust-1", RiskLevel = AiRiskLevel.Low, Summary = "No anomaly detected.", Recommendation = "Monitor", AnomalyScore = 30 });
        }

        public AiAgentOrchestrator Create(AiAgentOrchestrationOptions options) => new(
            Retry.Object,
            Security.Object,
            DuplicateReplay.Object,
            PayloadSchema.Object,
            EndpointRisk.Object,
            Anomaly.Object,
            LogSummary.Object,
            Transformation.Object,
            Options.Create(options),
            NullLogger<AiAgentOrchestrator>.Instance);
    }
}
