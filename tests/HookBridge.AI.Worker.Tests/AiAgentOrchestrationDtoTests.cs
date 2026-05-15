using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAgentOrchestrationDtoTests
{
    [Fact]
    public void RequestValidation_AcceptsEmptyTargetUrlButRequiresUtcReceivedAt()
    {
        var request = new AiAgentOrchestrationRequestDto
        {
            EventId = "evt-1",
            TargetUrl = string.Empty,
            ReceivedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        var results = Validate(request);

        results.Should().Contain(result => result.MemberNames.Contains(nameof(AiAgentOrchestrationRequestDto.ReceivedAtUtc)));
        results.Should().NotContain(result => result.MemberNames.Contains(nameof(AiAgentOrchestrationRequestDto.TargetUrl)));
    }

    [Fact]
    public void ResponseValidation_RejectsOutOfRangeConfidenceAndNonUtcGeneratedAt()
    {
        var response = new AiAgentOrchestrationResponseDto
        {
            ConfidenceScore = 1.2,
            GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var results = Validate(response);

        results.Should().Contain(result => result.MemberNames.Contains(nameof(AiAgentOrchestrationResponseDto.ConfidenceScore)));
        results.Should().Contain(result => result.MemberNames.Contains(nameof(AiAgentOrchestrationResponseDto.GeneratedAtUtc)));
    }

    [Fact]
    public void AgentResultValidation_RejectsNegativeConfidence()
    {
        var result = new AiAgentResultDto { ConfidenceScore = -0.1 };

        var results = Validate(result);

        results.Should().ContainSingle(validation => validation.MemberNames.Contains(nameof(AiAgentResultDto.ConfidenceScore)));
    }

    [Fact]
    public void OrchestrationResult_MapsResponseAndFallsBackForUnknownEnumStrings()
    {
        var request = new AiAgentOrchestrationRequestDto
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            CustomerId = "cust-1",
            RetryCount = 1,
            MaxRetryCount = 2,
            ReceivedAtUtc = DateTime.UtcNow
        };
        var response = new AiAgentOrchestrationResponseDto
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            OverallRiskLevel = AiRiskLevel.High,
            RecommendedAction = AiOrchestrationRecommendedAction.RequireManualReview,
            ConfidenceScore = 0.75,
            RequiresApproval = true,
            ApprovalId = "approval-1",
            GeneratedAtUtc = DateTime.UtcNow,
            AgentResults = [new AiAgentResultDto { AgentName = AiAgentName.SecurityAnalysisAgent, IsSuccessful = true, ConfidenceScore = 0.75 }]
        };

        var entity = AiAgentOrchestrationResult.FromResponse(response, request);
        entity.OverallRiskLevel = "not-a-risk";
        entity.RecommendedAction = "not-an-action";
        var mapped = entity.ToResponseDto();

        entity.CustomerId.Should().Be("cust-1");
        entity.ApprovalId.Should().Be("approval-1");
        mapped.OverallRiskLevel.Should().Be(AiRiskLevel.Unknown);
        mapped.RecommendedAction.Should().Be(AiOrchestrationRecommendedAction.None);
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
