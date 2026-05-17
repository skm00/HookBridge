using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.AutoRemediationRecommendation;
using HookBridge.AI.Worker.Services.RetryAgent;
using HookBridge.AI.Worker.Services.SafeMode;
using HookBridge.AI.Worker.Services.SecurityAgent;
using HookBridge.AI.Worker.Services.TransformationAgent;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSafeModeAgentIntegrationTests
{
    [Fact]
    public async Task RetryAgent_AppliesSafeModeDecisionToProductionRetryAction()
    {
        var safeMode = new RecordingSafeModeGuard(AiSafeModeDecision.RequiresApproval, allowed: false, requiresApproval: true);
        var agent = new RetryAgent(Options.Create(new RetryAgentOptions()), NullLogger<RetryAgent>.Instance, safeMode);

        var response = await agent.AnalyzeAsync(new RetryAgentRequestDto
        {
            EventId = "evt-retry",
            CorrelationId = "corr-retry",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "production",
            EventType = "WebhookDeliveryFailed",
            TargetUrl = "https://example.test/webhook",
            HttpMethod = "POST",
            StatusCode = 429,
            RetryCount = 1,
            MaxRetryCount = 5,
            FailedAtUtc = DateTime.UtcNow,
            EndpointRiskLevel = "Medium"
        });

        response.SafeModeDecision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.IsActionAllowed.Should().BeFalse();
        response.RequiresApproval.Should().BeTrue();
        safeMode.Requests.Should().ContainSingle(request => request.ActionType == AiActionType.RetryWebhook && request.EventId == "evt-retry");
    }


    [Fact]
    public async Task RetryAgent_DefaultsSafeModeEnvironmentToProductionWhenRequestEnvironmentIsMissing()
    {
        var safeMode = new RecordingSafeModeGuard(AiSafeModeDecision.RequiresApproval, allowed: false, requiresApproval: true);
        var agent = new RetryAgent(Options.Create(new RetryAgentOptions()), NullLogger<RetryAgent>.Instance, safeMode);

        var response = await agent.AnalyzeAsync(new RetryAgentRequestDto
        {
            EventId = "evt-retry-no-env",
            CorrelationId = "corr-retry-no-env",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            EventType = "WebhookDeliveryFailed",
            TargetUrl = "https://example.test/webhook",
            HttpMethod = "POST",
            StatusCode = 429,
            RetryCount = 1,
            MaxRetryCount = 5,
            FailedAtUtc = DateTime.UtcNow,
            EndpointRiskLevel = "Medium"
        });

        response.SafeModeDecision.Should().Be(AiSafeModeDecision.RequiresApproval);
        safeMode.Requests.Should().ContainSingle(request =>
            request.ActionType == AiActionType.RetryWebhook &&
            request.EventId == "evt-retry-no-env" &&
            request.Environment == "production");
    }

    [Fact]
    public async Task SecurityAgent_AppliesSafeModeDecisionToQuarantineAction()
    {
        var safeMode = new RecordingSafeModeGuard(AiSafeModeDecision.RequiresApproval, allowed: false, requiresApproval: true);
        var agent = new SecurityAgent(Options.Create(new SecurityAgentOptions()), NullLogger<SecurityAgent>.Instance, safeModeGuard: safeMode);

        var response = await agent.AnalyzeAsync(new SecurityAgentRequestDto
        {
            EventId = "evt-security",
            CorrelationId = "corr-security",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "production",
            TargetUrl = "https://example.test/webhook",
            IsReplay = true,
            Payload = "replay signal that should be quarantined",
            ReceivedAtUtc = DateTime.UtcNow
        });

        response.SecurityDecision.Should().Be(SecurityAgentDecision.Quarantine);
        response.SafeModeDecision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.IsActionAllowed.Should().BeFalse();
        safeMode.Requests.Should().ContainSingle(request => request.ActionType == AiActionType.QuarantineEvent && request.EventId == "evt-security");
    }

    [Fact]
    public async Task TransformationAgent_AppliesSafeModeDecisionToGeneratedCode()
    {
        var safeMode = new RecordingSafeModeGuard(AiSafeModeDecision.RequiresApproval, allowed: false, requiresApproval: true);
        var agent = new TransformationAgent(
            Options.Create(new TransformationAgentOptions()),
            new StubTransformationRecommendationAgent(),
            NullLogger<TransformationAgent>.Instance,
            safeMode);

        var response = await agent.AnalyzeAsync(new TransformationAgentRequestDto
        {
            EventId = "evt-transform",
            CorrelationId = "corr-transform",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "production",
            SourcePayload = """{"id":"1"}""",
            TargetSamplePayload = """{"id":"string"}""",
            ReceivedAtUtc = DateTime.UtcNow
        });

        response.SafeModeDecision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.IsActionAllowed.Should().BeFalse();
        response.RequiresApproval.Should().BeTrue();
        safeMode.Requests.Should().ContainSingle(request => request.ActionType == AiActionType.ApplyTransformation && request.EventId == "evt-transform");
    }

    [Fact]
    public async Task AutoRemediationRecommendation_DisablesAutoApplyWhenSafeModeBlocksAction()
    {
        var safeMode = new RecordingSafeModeGuard(AiSafeModeDecision.Blocked, allowed: false, requiresApproval: false);
        var service = new AutoRemediationRecommendationService(
            Options.Create(new AutoRemediationRecommendationOptions { AllowAutoApplyLowRisk = true }),
            NullLogger<AutoRemediationRecommendationService>.Instance,
            safeMode);

        var response = await service.RecommendAsync(new AutoRemediationRecommendationRequestDto
        {
            EventId = "evt-remediation",
            CorrelationId = "corr-remediation",
            CustomerId = "cust-1",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "production",
            RiskLevel = "Low",
            ConfidenceScore = 0.95,
            StatusCode = 429,
            CreatedAtUtc = DateTime.UtcNow
        });

        response.SafeModeDecision.Should().Be(AiSafeModeDecision.Blocked);
        response.IsActionAllowed.Should().BeFalse();
        response.CanAutoApply.Should().BeFalse();
        safeMode.Requests.Should().ContainSingle(request => request.ActionType == AiActionType.RetryWebhook && request.EventId == "evt-remediation");
    }

    private sealed class RecordingSafeModeGuard : IAiSafeModeGuard
    {
        private readonly AiSafeModeDecision _decision;
        private readonly bool _allowed;
        private readonly bool _requiresApproval;

        public RecordingSafeModeGuard(AiSafeModeDecision decision, bool allowed, bool requiresApproval)
        {
            _decision = decision;
            _allowed = allowed;
            _requiresApproval = requiresApproval;
        }

        public List<AiSafeModeEvaluationRequestDto> Requests { get; } = [];

        public Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new AiSafeModeEvaluationResponseDto
            {
                Decision = _decision,
                IsAllowed = _allowed,
                RequiresApproval = _requiresApproval,
                Reason = "safe mode test decision",
                BlockMessage = _allowed ? null : "blocked by safe mode",
                ActionType = request.ActionType,
                Environment = request.Environment,
                EvaluatedAtUtc = DateTime.UtcNow
            });
        }
    }

    private sealed class StubTransformationRecommendationAgent : IWebhookTransformationRecommendationAgent
    {
        public Task<WebhookTransformationRecommendationResponseDto> RecommendAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new WebhookTransformationRecommendationResponseDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                RiskLevel = "Low",
                ConfidenceScore = 0.9,
                RecommendedMappings =
                [
                    new WebhookFieldMappingRecommendationDto
                    {
                        SourceJsonPath = "$.id",
                        TargetJsonPath = "$.id",
                        SourceFieldName = "id",
                        TargetFieldName = "id",
                        TransformationType = WebhookTransformationType.DirectMap,
                        ConfidenceScore = 0.9
                    }
                ],
                GeneratedTransformationCode = "target.id = source.id;",
                GeneratedAtUtc = DateTime.UtcNow
            });
    }
}
