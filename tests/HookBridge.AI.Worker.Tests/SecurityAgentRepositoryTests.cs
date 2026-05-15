using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Tests;

public sealed class SecurityAgentRepositoryTests
{
    [Fact]
    public void FromResponse_MapsResponseAndRequest()
    {
        var request = new SecurityAgentRequestDto { EventId = "evt-1", CustomerId = "cust-1", ReceivedAtUtc = DateTime.UtcNow, IsReplay = true };
        var response = new SecurityAgentResponseDto { EventId = "evt-1", SecurityDecision = SecurityAgentDecision.Quarantine, RiskLevel = AiRiskLevel.High, SecurityRiskScore = 70, RequiresApproval = true, GeneratedAtUtc = DateTime.UtcNow };

        var result = SecurityAgentResult.FromResponse(response, request);

        result.EventId.Should().Be("evt-1");
        result.CustomerId.Should().Be("cust-1");
        result.SecurityDecision.Should().Be(SecurityAgentDecision.Quarantine);
        result.RiskLevel.Should().Be(AiRiskLevel.High);
        result.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void IndexCreationLogic_IncludesRequiredIndexes()
    {
        var names = AiMongoIndexInitializer.CreateSecurityAgentResultIndexModels()
            .Select(index => index.Options?.Name)
            .ToArray();

        names.Should().Contain([
            "idx_security_agent_results_event_id",
            "idx_security_agent_results_correlation_id",
            "idx_security_agent_results_customer_id",
            "idx_security_agent_results_subscription_id",
            "idx_security_agent_results_endpoint_id",
            "idx_security_agent_results_environment",
            "idx_security_agent_results_security_decision",
            "idx_security_agent_results_risk_level",
            "idx_security_agent_results_is_suspicious",
            "idx_security_agent_results_requires_approval",
            "idx_security_agent_results_generated_at_utc_desc"]);
    }
}
