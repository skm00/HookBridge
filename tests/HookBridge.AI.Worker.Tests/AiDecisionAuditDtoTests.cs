using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDecisionAuditDtoTests
{
    [Fact]
    public void AiDecisionAuditOptions_DefaultsMatchProductionSafeAuditBehavior()
    {
        var options = new AiDecisionAuditOptions();

        options.Enabled.Should().BeTrue();
        options.AuditFallbackDecisions.Should().BeTrue();
        options.AuditSafeModeEvaluations.Should().BeTrue();
        options.AuditHumanApprovals.Should().BeTrue();
        options.AuditNaturalLanguageQueries.Should().BeTrue();
        options.MaxMetadataLength.Should().Be(4000);
        options.IncludePromptMetadata.Should().BeTrue();
        options.IncludeModelMetadata.Should().BeTrue();
    }

    [Fact]
    public void AiMongoOptions_DefaultDecisionAuditCollectionNameIsConfigured()
    {
        var options = new AiMongoOptions();

        options.AiDecisionAuditRecordsCollectionName.Should().Be("ai_decision_audit_records");
        AiMongoOptions.DefaultAiDecisionAuditRecordsCollectionName.Should().Be("ai_decision_audit_records");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void CreateRequestDto_RejectsConfidenceOutsideRange(double confidenceScore)
    {
        var request = new AiDecisionAuditCreateRequestDto
        {
            DecisionType = AiDecisionAuditType.RetryDecision,
            ConfidenceScore = confidenceScore
        };

        var results = Validate(request);

        results.Should().Contain(result => result.MemberNames.Contains(nameof(AiDecisionAuditCreateRequestDto.ConfidenceScore)));
    }

    [Fact]
    public void CreateRequestDto_RequiresDecisionType()
    {
        var request = new AiDecisionAuditCreateRequestDto();

        var results = Validate(request);

        results.Should().Contain(result => result.MemberNames.Contains(nameof(AiDecisionAuditCreateRequestDto.DecisionType)));
    }

    [Fact]
    public void Record_ToResponseDto_CopiesAuditFields()
    {
        var now = DateTime.UtcNow;
        var record = new AiDecisionAuditRecord
        {
            Id = "507f1f77bcf86cd799439011",
            AuditId = "aud_1",
            EventId = "evt_1",
            CorrelationId = "corr_1",
            CustomerId = "cust_1",
            CustomerIdType = "tenant",
            SubscriptionId = "sub_1",
            EndpointId = "endpoint_1",
            Environment = "qa",
            AgentName = "RetryAgent",
            DecisionType = AiDecisionAuditType.RetryDecision,
            Decision = "RetryWithBackoff",
            RiskLevel = "Medium",
            ConfidenceScore = 0.8,
            ConfidenceLevel = "High",
            SuggestedAction = "RetryWithBackoff",
            RequiresApproval = false,
            ApprovalId = "approval_1",
            ApprovalStatus = "Pending",
            SafeModeDecision = "Allowed",
            IsActionAllowed = true,
            UsedAi = true,
            UsedFallback = false,
            FallbackReason = "None",
            PromptName = "prompt",
            PromptVersion = "v1.0.0",
            PromptHash = "sha256:abc",
            Model = "llama3",
            Provider = "Ollama",
            Summary = "summary",
            Recommendation = "recommendation",
            ReasonCodes = ["RateLimited"],
            CreatedBy = "test",
            CreatedAtUtc = now,
            Metadata = new Dictionary<string, string?> { ["safe"] = "value" }
        };

        var response = record.ToResponseDto();

        response.AuditId.Should().Be(record.AuditId);
        response.EventId.Should().Be(record.EventId);
        response.DecisionType.Should().Be(record.DecisionType);
        response.ReasonCodes.Should().ContainSingle("RateLimited");
        response.Metadata["safe"].Should().Be("value");
        response.CreatedAtUtc.Should().Be(now);
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
