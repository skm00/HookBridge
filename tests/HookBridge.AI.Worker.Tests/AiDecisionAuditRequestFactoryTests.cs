using FluentAssertions;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDecisionAuditRequestFactoryTests
{
    [Fact]
    public void FromRetry_MapsDecisionContext()
    {
        var audit = AiDecisionAuditRequestFactory.FromRetry(new RetryAgentResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            RetryDecision = RetryAgentDecision.RetryWithExponentialBackoff,
            RetryDelaySeconds = 30,
            MaxAllowedRetries = 5,
            RiskLevel = "Medium",
            RequiresApproval = true,
            SafeModeDecision = AiSafeModeDecision.RequiresApproval,
            IsActionAllowed = false,
            ConfidenceScore = 0.82,
            ConfidenceLevel = AiConfidenceLevel.High,
            Fallback = true,
            Summary = "summary",
            Recommendation = "recommendation",
            ReasonCodes = [RetryAgentReasonCode.RateLimited]
        }, CreateRetryRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.RetryDecision);
        audit.AgentName.Should().Be("RetryAgent");
        audit.Decision.Should().Be(nameof(RetryAgentDecision.RetryWithExponentialBackoff));
        audit.UsedFallback.Should().BeTrue();
        audit.FallbackReason.Should().Be("AgentDisabledOrFallback");
        audit.Metadata["retryDelaySeconds"].Should().Be("30");
        audit.Metadata["maxAllowedRetries"].Should().Be("5");
        audit.ReasonCodes.Should().Contain(nameof(RetryAgentReasonCode.RateLimited));
    }

    [Fact]
    public void FromSecurity_MapsSecurityFieldsWithoutPayloadOrHeaders()
    {
        var audit = AiDecisionAuditRequestFactory.FromSecurity(new SecurityAgentResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            SecurityDecision = SecurityAgentDecision.Quarantine,
            SecurityRiskScore = 90,
            IsSuspicious = true,
            RiskLevel = AiRiskLevel.Critical,
            RequiresApproval = true,
            ConfidenceScore = 0.74,
            ConfidenceLevel = AiConfidenceLevel.High,
            Summary = "security summary",
            Recommendation = "quarantine",
            ReasonCodes = [SecurityAgentReasonCode.SuspiciousPayload]
        }, CreateSecurityRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.SecurityDecision);
        audit.Decision.Should().Be(nameof(SecurityAgentDecision.Quarantine));
        audit.RiskLevel.Should().Be(nameof(AiRiskLevel.Critical));
        audit.Metadata["securityRiskScore"].Should().Be("90");
        audit.Metadata["isSuspicious"].Should().Be("True");
        audit.Metadata.Keys.Should().NotContain("Headers");
        audit.Metadata.Keys.Should().NotContain("Payload");
        audit.Metadata.Keys.Should().NotContain("rawPayload");
    }

    [Fact]
    public void FromTransformation_MapsPromptMetadataAndReasonCodes()
    {
        var audit = AiDecisionAuditRequestFactory.FromTransformation(new TransformationAgentResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            TransformationDecision = TransformationAgentDecision.MappingReady,
            RiskLevel = "Low",
            ConfidenceScore = 0.91,
            ConfidenceLevel = AiConfidenceLevel.VeryHigh,
            PromptName = "transform",
            PromptVersion = "v1.2.3",
            PromptHash = "sha256:abc",
            ReasonCodes = [TransformationAgentReasonCode.DirectMappingAvailable]
        }, CreateTransformationRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.TransformationDecision);
        audit.PromptName.Should().Be("transform");
        audit.PromptVersion.Should().Be("v1.2.3");
        audit.PromptHash.Should().Be("sha256:abc");
        audit.ReasonCodes.Should().Contain(nameof(TransformationAgentReasonCode.DirectMappingAvailable));
    }

    [Fact]
    public void FromObservability_MapsSuggestedActionsAndPromptMetadata()
    {
        var audit = AiDecisionAuditRequestFactory.FromObservability(new ObservabilityAgentResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            ObservabilityStatus = ObservabilityStatus.Critical,
            RiskLevel = AiRiskLevel.High,
            ConfidenceScore = 0.71,
            ConfidenceLevel = AiConfidenceLevel.High,
            SuggestedActions = [ObservabilitySuggestedAction.CheckKafkaLag, ObservabilitySuggestedAction.EscalateToSupport],
            PromptName = "observability-agent",
            PromptVersion = "v1.0.0",
            PromptHash = "sha256:def"
        }, CreateObservabilityRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.ObservabilityDecision);
        audit.Decision.Should().Be(nameof(ObservabilityStatus.Critical));
        audit.SuggestedAction.Should().Be("CheckKafkaLag,EscalateToSupport");
        audit.PromptHash.Should().Be("sha256:def");
    }

    [Fact]
    public void FromOrchestration_MapsFallbackAndApprovalContext()
    {
        var audit = AiDecisionAuditRequestFactory.FromOrchestration(new AiAgentOrchestrationResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            RecommendedAction = AiOrchestrationRecommendedAction.RetryWithBackoff,
            OverallRiskLevel = AiRiskLevel.Medium,
            ConfidenceScore = 0.66,
            ConfidenceLevel = AiConfidenceLevel.Medium,
            RequiresApproval = true,
            ApprovalId = "app_1",
            SafeModeDecision = AiSafeModeDecision.RequiresApproval,
            IsActionAllowed = false,
            AgentResults = [new AiAgentResultDto { AgentName = AiAgentName.SecurityAgent, UsedFallback = true }]
        }, CreateOrchestrationRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.OrchestrationDecision);
        audit.ApprovalId.Should().Be("app_1");
        audit.UsedFallback.Should().BeTrue();
        audit.Metadata["agentCount"].Should().Be("1");
    }

    [Fact]
    public void FromAutoRemediation_MapsRecommendationContext()
    {
        var audit = AiDecisionAuditRequestFactory.FromAutoRemediation(new AutoRemediationRecommendationResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            RemediationType = AutoRemediationType.RetryTuning,
            RecommendedAction = AutoRemediationRecommendedAction.RetryWithBackoff,
            RiskLevel = "Medium",
            ConfidenceScore = 0.8,
            CanAutoApply = true,
            ReasonCodes = [AutoRemediationReasonCode.RateLimited]
        }, CreateAutoRemediationRequest());

        audit.DecisionType.Should().Be(AiDecisionAuditType.AutoRemediationRecommendation);
        audit.Decision.Should().Be(nameof(AutoRemediationRecommendedAction.RetryWithBackoff));
        audit.Metadata["remediationType"].Should().Be(nameof(AutoRemediationType.RetryTuning));
        audit.Metadata["canAutoApply"].Should().Be("True");
        audit.ReasonCodes.Should().Contain(nameof(AutoRemediationReasonCode.RateLimited));
    }

    private static RetryAgentRequestDto CreateRetryRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa"
    };

    private static SecurityAgentRequestDto CreateSecurityRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa",
        Headers = new Dictionary<string, string> { ["Authorization"] = "secret" },
        Payload = new { password = "secret" }
    };

    private static TransformationAgentRequestDto CreateTransformationRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa"
    };

    private static ObservabilityAgentRequestDto CreateObservabilityRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa"
    };

    private static AiAgentOrchestrationRequestDto CreateOrchestrationRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa"
    };

    private static AutoRemediationRecommendationRequestDto CreateAutoRemediationRequest() => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa"
    };
}
