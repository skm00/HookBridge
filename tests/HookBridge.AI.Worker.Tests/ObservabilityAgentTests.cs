using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.ObservabilityAgent;
using ObservabilityAgentService = HookBridge.AI.Worker.Services.ObservabilityAgent.ObservabilityAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class ObservabilityAgentTests
{
    [Fact]
    public async Task HealthyTelemetry_ReturnsHealthyLowRisk()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest());
        response.ObservabilityStatus.Should().Be(ObservabilityStatus.Healthy);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.SuggestedActions.Should().Contain(ObservabilitySuggestedAction.Monitor);
    }

    [Fact]
    public async Task KafkaLagDegraded_ReturnsDegraded()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(kafkaLag: 1200));
        response.ObservabilityStatus.Should().Be(ObservabilityStatus.Degraded);
        response.Signals.Should().Contain(signal => signal.SignalName == "KafkaConsumerLag");
        response.SuggestedActions.Should().Contain(ObservabilitySuggestedAction.CheckKafkaLag);
    }

    [Fact]
    public async Task KafkaLagCritical_ReturnsCriticalAndRequiresApproval()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(kafkaLag: 12000));
        response.ObservabilityStatus.Should().Be(ObservabilityStatus.Critical);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task MongoUnhealthy_ReturnsCritical()
    {
        var request = CreateRequest();
        request.MongoIsHealthy = false;
        var response = await CreateAgent().AnalyzeAsync(request);
        response.ObservabilityStatus.Should().Be(ObservabilityStatus.Critical);
        response.SuggestedActions.Should().Contain(ObservabilitySuggestedAction.CheckMongoHealth);
    }

    [Theory]
    [InlineData(1500, ObservabilityStatus.Degraded)]
    [InlineData(6000, ObservabilityStatus.Unhealthy)]
    public async Task MongoLatency_MapsStatus(long latency, ObservabilityStatus expected)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(mongoLatency: latency));
        response.ObservabilityStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData(11, ObservabilityStatus.Degraded)]
    [InlineData(31, ObservabilityStatus.Unhealthy)]
    public async Task FailureRate_MapsStatus(long failed, ObservabilityStatus expected)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(total: 100, failed: failed));
        response.ObservabilityStatus.Should().Be(expected);
        response.Signals.Should().Contain(signal => signal.SignalName == "FailureRate");
    }

    [Theory]
    [InlineData("dead-letter")]
    [InlineData("anomaly")]
    [InlineData("security")]
    [InlineData("errors")]
    public async Task CountSignals_CreateExpectedSignals(string mode)
    {
        var request = CreateRequest();
        if (mode == "dead-letter") request.DeadLetterCount = 1;
        if (mode == "anomaly") request.AnomalyCount = 1;
        if (mode == "security") request.SecurityFindingCount = 1;
        if (mode == "errors") request.ErrorLogCount = 1;

        var response = await CreateAgent().AnalyzeAsync(request);

        response.ObservabilityStatus.Should().Be(ObservabilityStatus.Degraded);
        response.Signals.Should().NotBeEmpty();
        response.SuggestedActions.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(ObservabilityStatus.Unknown, AiRiskLevel.Unknown)]
    [InlineData(ObservabilityStatus.Healthy, AiRiskLevel.Low)]
    [InlineData(ObservabilityStatus.Degraded, AiRiskLevel.Medium)]
    [InlineData(ObservabilityStatus.Unhealthy, AiRiskLevel.High)]
    [InlineData(ObservabilityStatus.Critical, AiRiskLevel.Critical)]
    public void MapRiskLevel_ReturnsExpectedRisk(ObservabilityStatus status, AiRiskLevel expected)
        => ObservabilityAgentService.MapRiskLevel(status).Should().Be(expected);

    [Fact]
    public async Task CriticalErrorCount_ClampsConfidenceAndGeneratedUtc()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(errorCount: 1000));
        response.ConfidenceScore.Should().BeInRange(0, 1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ShouldPublishAnomaly_ReturnsTrueForUnhealthyAndCritical()
    {
        ObservabilityAgentService.ShouldPublishAnomaly(new ObservabilityAgentResponseDto { ObservabilityStatus = ObservabilityStatus.Unhealthy }).Should().BeTrue();
        ObservabilityAgentService.ShouldPublishAnomaly(new ObservabilityAgentResponseDto { ObservabilityStatus = ObservabilityStatus.Critical }).Should().BeTrue();
        ObservabilityAgentService.ShouldPublishAnomaly(new ObservabilityAgentResponseDto { ObservabilityStatus = ObservabilityStatus.Degraded }).Should().BeFalse();
    }

    [Fact]
    public async Task InvalidWindow_ThrowsValidationException()
    {
        var request = CreateRequest();
        request.EvaluationWindowToUtc = request.EvaluationWindowFromUtc;
        var act = async () => await CreateAgent().AnalyzeAsync(request);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task NegativeMetric_ThrowsValidationException()
    {
        var request = CreateRequest();
        request.RetryCount = -1;
        var act = async () => await CreateAgent().AnalyzeAsync(request);
        await act.Should().ThrowAsync<ValidationException>();
    }

    private static ObservabilityAgentService CreateAgent(ObservabilityAgentOptions? options = null)
        => new(Options.Create(options ?? new ObservabilityAgentOptions { EnableAiLogSummary = false }), NullLogger<ObservabilityAgentService>.Instance);

    private static ObservabilityAgentRequestDto CreateRequest(long kafkaLag = 0, long mongoLatency = 50, long total = 100, long failed = 0, int errorCount = 0) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        Environment = "qa",
        ServiceName = "HookBridge.AI.Worker",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        KafkaConsumerLag = kafkaLag,
        KafkaTopic = "hookbridge.ai.analysis",
        KafkaConsumerGroupId = "hookbridge-ai-worker",
        MongoIsHealthy = true,
        MongoLatencyMs = mongoLatency,
        TotalDeliveries = total,
        FailedDeliveries = failed,
        ErrorLogCount = errorCount,
        EvaluationWindowFromUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
        EvaluationWindowToUtc = new DateTime(2026, 5, 14, 10, 15, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 5, 14, 10, 16, 0, DateTimeKind.Utc)
    };
}
