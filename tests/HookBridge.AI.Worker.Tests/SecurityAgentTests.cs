using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.SecurityAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class SecurityAgentTests
{
    [Fact]
    public async Task SafePayload_ReturnsAllowOrMonitor()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest());
        response.SecurityDecision.Should().BeOneOf(SecurityAgentDecision.Allow, SecurityAgentDecision.Monitor);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.RequiresApproval.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, SecurityAgentReasonCode.SignatureValidationFailed, 30)]
    [InlineData(false, true, SecurityAgentReasonCode.AuthenticationFailed, 30)]
    public async Task AuthAndSignatureFailures_IncreaseScoreAndNeverAllow(bool signatureFailed, bool authFailed, SecurityAgentReasonCode reason, int expectedScore)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(signatureFailed: signatureFailed, authFailed: authFailed));
        response.SecurityRiskScore.Should().BeGreaterThanOrEqualTo(expectedScore);
        response.ReasonCodes.Should().Contain(reason);
        response.SecurityDecision.Should().NotBe(SecurityAgentDecision.Allow);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task ReplayDetected_ReturnsQuarantineAndRequiresApproval()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(isReplay: true));
        response.SecurityDecision.Should().BeOneOf(SecurityAgentDecision.Quarantine, SecurityAgentDecision.Reject);
        response.ReasonCodes.Should().Contain(SecurityAgentReasonCode.ReplayDetected);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task DuplicateDetected_ReturnsMonitorOrManualReview()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(isDuplicate: true));
        response.SecurityDecision.Should().BeOneOf(SecurityAgentDecision.Monitor, SecurityAgentDecision.RequireManualReview);
        response.ReasonCodes.Should().Contain(SecurityAgentReasonCode.DuplicateDetected);
    }

    [Theory]
    [InlineData("<script>alert('x')</script>", SecurityAgentReasonCode.ScriptContentDetected)]
    [InlineData("' UNION SELECT password FROM users", SecurityAgentReasonCode.SqlInjectionPattern)]
    [InlineData("; rm -rf /", SecurityAgentReasonCode.CommandInjectionPattern)]
    [InlineData("../../etc/passwd", SecurityAgentReasonCode.PathTraversalPattern)]
    [InlineData("client_secret=abc123", SecurityAgentReasonCode.SecretValueDetected)]
    public async Task PayloadPatternDetection_AddsExpectedReasonCode(string payload, SecurityAgentReasonCode expected)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(payload: payload));
        response.ReasonCodes.Should().Contain(expected);
        response.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public async Task LargePayloadDetection_AddsReasonCode()
    {
        var response = await CreateAgent(new SecurityAgentOptions { LargePayloadThresholdBytes = 10 }).AnalyzeAsync(CreateRequest(payloadSizeBytes: 11));
        response.ReasonCodes.Should().Contain(SecurityAgentReasonCode.LargePayload);
    }

    [Fact]
    public async Task SuspiciousUserAgentDetection_AddsReasonCode()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(userAgent: "sqlmap/1.0"));
        response.ReasonCodes.Should().Contain(SecurityAgentReasonCode.SuspiciousUserAgent);
    }

    [Fact]
    public async Task ScoreIsClampedBetweenZeroAndOneHundred()
    {
        var response = await CreateAgent(new SecurityAgentOptions { LargePayloadThresholdBytes = 1 }).AnalyzeAsync(CreateRequest(signatureFailed: true, authFailed: true, isReplay: true, isDuplicate: true, payload: "<script> UNION SELECT ; rm -rf / ../../ client_secret=x", payloadSizeBytes: 99, userAgent: "sqlmap"));
        response.SecurityRiskScore.Should().Be(100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
    }

    [Theory]
    [InlineData(0, AiRiskLevel.Low)]
    [InlineData(20, AiRiskLevel.Low)]
    [InlineData(21, AiRiskLevel.Medium)]
    [InlineData(50, AiRiskLevel.Medium)]
    [InlineData(51, AiRiskLevel.High)]
    [InlineData(80, AiRiskLevel.High)]
    [InlineData(81, AiRiskLevel.Critical)]
    [InlineData(100, AiRiskLevel.Critical)]
    public void RiskLevelMapping_Works(int score, AiRiskLevel expected)
        => SecurityAgent.MapRiskLevel(score).Should().Be(expected);

    [Fact]
    public void DecisionMapping_AuthFailureNeverAllows()
    {
        var decision = SecurityAgent.MapDecision(AiRiskLevel.Low, new HashSet<SecurityAgentReasonCode> { SecurityAgentReasonCode.AuthenticationFailed });
        decision.Should().NotBe(SecurityAgentDecision.Allow);
    }

    [Theory]
    [InlineData("<script>x</script>")]
    [InlineData("UNION SELECT * FROM users; rm -rf / ../../etc/passwd")]
    public async Task HighOrCriticalRisk_RequiresApproval(string payload)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(payload: payload, signatureFailed: true));
        response.RiskLevel.Should().BeOneOf(AiRiskLevel.High, AiRiskLevel.Critical);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task GeneratedAtUtc_IsUtcAndConfidenceIsClamped()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest());
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.ConfidenceScore.Should().BeInRange(0, 1);
    }

    [Fact]
    public void InvalidTargetUrl_FailsValidation()
    {
        var request = CreateRequest(targetUrl: "not-a-url");
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true).Should().BeFalse();
    }

    [Fact]
    public async Task LowRisk_DoesNotPublishAnomalyByPolicy()
    {
        var agent = CreateAgent();
        var response = await agent.AnalyzeAsync(CreateRequest());
        agent.ShouldPublishAnomaly(response).Should().BeFalse();
    }

    [Fact]
    public async Task HighCriticalRisk_PublishesAnomalyWhenConfigured()
    {
        var agent = CreateAgent();
        var response = await agent.AnalyzeAsync(CreateRequest(signatureFailed: true, authFailed: true));
        response.RiskLevel.Should().Be(AiRiskLevel.High);
        agent.ShouldPublishAnomaly(response).Should().BeTrue();
    }

    private static SecurityAgent CreateAgent(SecurityAgentOptions? options = null)
        => new(Options.Create(options ?? new SecurityAgentOptions()), NullLogger<SecurityAgent>.Instance);

    private static SecurityAgentRequestDto CreateRequest(
        bool signatureFailed = false,
        bool authFailed = false,
        bool isDuplicate = false,
        bool isReplay = false,
        object? payload = null,
        long payloadSizeBytes = 128,
        string? userAgent = "HookBridge-Test",
        string? targetUrl = "https://example.test/webhook") => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        TargetUrl = targetUrl,
        UserAgent = userAgent,
        SignatureValidationFailed = signatureFailed,
        AuthenticationFailed = authFailed,
        IsDuplicate = isDuplicate,
        IsReplay = isReplay,
        Payload = payload ?? new { id = "evt-1", ok = true },
        PayloadSizeBytes = payloadSizeBytes,
        ReceivedAtUtc = DateTime.UtcNow
    };
}
