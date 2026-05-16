using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Mapping;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.Confidence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiConfidenceScoreServiceTests
{
    private readonly AiConfidenceScoreService _service = new(
        Options.Create(new AiConfidenceScoreOptions()),
        NullLogger<AiConfidenceScoreService>.Instance);

    [Fact]
    public void Calculate_BaseConfidenceScore_UsesDefaultBase()
    {
        var result = _service.Calculate(Request(riskLevel: AiRiskLevel.Low));
        result.ConfidenceScore.Should().Be(0.75);
        result.ConfidenceLevel.Should().Be(AiConfidenceLevel.High);
    }

    [Fact]
    public void Calculate_ValidAiJson_IncreasesConfidence()
        => _service.Calculate(Request(usedAi: true, validJson: true, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.85);

    [Fact]
    public void Calculate_RequiredFields_IncreasesConfidence()
        => _service.Calculate(Request(requiredFields: true, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.80);

    [Fact]
    public void Calculate_EvidenceCount_IncreasesConfidence()
        => _service.Calculate(Request(evidenceCount: 3, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.80);

    [Fact]
    public void Calculate_FallbackUsage_ReducesConfidence()
        => _service.Calculate(Request(usedFallback: true, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.60);

    [Fact]
    public void Calculate_MissingData_ReducesConfidence()
        => _service.Calculate(Request(missingDataCount: 2, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.65);

    [Fact]
    public void Calculate_ValidationIssues_ReducesConfidence()
        => _service.Calculate(Request(validationIssueCount: 2, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.65);

    [Fact]
    public void Calculate_FailedAgents_ReducesConfidence()
        => _service.Calculate(Request(failedAgentCount: 2, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.55);

    [Fact]
    public void Calculate_UnknownRisk_ReducesConfidence()
        => _service.Calculate(Request()).ConfidenceScore.Should().Be(0.65);

    [Fact]
    public void Calculate_RuleBasedStrongEvidence_IncreasesConfidence()
        => _service.Calculate(Request(isRuleBased: true, evidenceCount: 1, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(0.80);

    [Fact]
    public void Calculate_ClampsScoreToZero()
    {
        var service = new AiConfidenceScoreService(Options.Create(new AiConfidenceScoreOptions { BaseScore = 0 }), NullLogger<AiConfidenceScoreService>.Instance);
        service.Calculate(Request(usedFallback: true, missingDataCount: 99, validationIssueCount: 99, failedAgentCount: 99)).ConfidenceScore.Should().Be(0);
    }

    [Fact]
    public void Calculate_ClampsScoreToOne()
        => _service.Calculate(Request(usedAi: true, validJson: true, requiredFields: true, evidenceCount: 3, isRuleBased: true, riskLevel: AiRiskLevel.Low)).ConfidenceScore.Should().Be(1);

    [Theory]
    [InlineData(0.39, AiConfidenceLevel.Low)]
    [InlineData(0.40, AiConfidenceLevel.Medium)]
    [InlineData(0.70, AiConfidenceLevel.High)]
    [InlineData(0.90, AiConfidenceLevel.VeryHigh)]
    public void MapConfidenceLevel_MapsThresholds(double score, AiConfidenceLevel expected)
        => _service.MapConfidenceLevel(score).Should().Be(expected);

    [Fact]
    public void RequiresManualReview_LowConfidenceRequiresManualReview()
        => _service.RequiresManualReview(0.59, AiRiskLevel.Low).Should().BeTrue();

    [Fact]
    public void RequiresNeedsMoreInfoOrManualReview_VeryLowConfidenceReturnsTrue()
        => _service.RequiresNeedsMoreInfoOrManualReview(0.39).Should().BeTrue();

    [Fact]
    public void Calculate_FallbackConfidenceRange_IsMediumRange()
    {
        var result = _service.Calculate(Request(usedFallback: true, isRuleBased: true, evidenceCount: 3, riskLevel: AiRiskLevel.Medium));
        result.ConfidenceScore.Should().BeInRange(0.50, 0.75);
    }

    [Fact]
    public void OrchestrationConfidence_WithFailedAgents_IsPenalized()
    {
        var results = new[]
        {
            new AiAgentResultDto { IsSuccessful = true, ConfidenceScore = 0.9 },
            new AiAgentResultDto { IsSuccessful = false, ConfidenceScore = 0 }
        };
        HookBridge.AI.Worker.Services.Orchestration.AiAgentOrchestrator.CalculateConfidence(results).Should().Be(0.85);
    }

    [Fact]
    public void OrchestrationConfidence_WithFallbackAgents_IsPenalized()
    {
        var results = new[] { new AiAgentResultDto { IsSuccessful = true, ConfidenceScore = 0.9, UsedFallback = true } };
        HookBridge.AI.Worker.Services.Orchestration.AiAgentOrchestrator.CalculateConfidence(results).Should().Be(0.87);
    }

    [Fact]
    public void Mapper_PersistsConfidenceFieldsToMongoResult()
    {
        var response = new WebhookFailureAnalysisResponseDto
        {
            EventId = "evt_1",
            RiskLevel = AiRiskLevel.Medium,
            ConfidenceScore = 0.82,
            ConfidenceLevel = AiConfidenceLevel.High,
            ConfidenceExplanation = "Evidence supported the decision."
        };

        var result = WebhookFailureAnalysisMapper.ToAiAnalysisResult(response);

        result.ConfidenceScore.Should().Be(0.82);
        result.ConfidenceLevel.Should().Be("High");
        result.ConfidenceExplanation.Should().Be("Evidence supported the decision.");
    }

    [Fact]
    public void ServiceCollection_RegistersConfidenceServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection().AddLogging().AddAiOptions(configuration).AddAiRetryRecommendationServices();
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAiConfidenceScoreService>().Should().BeOfType<AiConfidenceScoreService>();
        provider.GetRequiredService<IOptions<AiConfidenceScoreOptions>>().Value.Enabled.Should().BeTrue();
    }

    private static AiConfidenceScoreRequestDto Request(
        bool usedFallback = false,
        bool usedAi = false,
        bool isRuleBased = false,
        int evidenceCount = 0,
        int missingDataCount = 0,
        int validationIssueCount = 0,
        int failedAgentCount = 0,
        bool validJson = false,
        bool requiredFields = false,
        AiRiskLevel riskLevel = AiRiskLevel.Unknown)
        => new()
        {
            DecisionType = AiDecisionType.RetryDecision,
            RiskLevel = riskLevel,
            UsedFallback = usedFallback,
            UsedAi = usedAi,
            IsRuleBased = isRuleBased,
            EvidenceCount = evidenceCount,
            MissingDataCount = missingDataCount,
            ValidationIssueCount = validationIssueCount,
            FailedAgentCount = failedAgentCount,
            TotalAgentCount = Math.Max(1, failedAgentCount),
            LlmResponseWasValidJson = validJson,
            LlmResponseHadRequiredFields = requiredFields,
            CreatedAtUtc = DateTime.UtcNow
        };
}
