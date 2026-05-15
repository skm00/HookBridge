using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.RetryAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class RetryAgentTests
{

    [Fact]
    public void RetryAgentOptions_DefaultsMatchSafetyConfiguration()
    {
        var options = new RetryAgentOptions();
        options.Enabled.Should().BeTrue();
        options.BaseDelaySeconds.Should().Be(30);
        options.MaxDelaySeconds.Should().Be(3600);
        options.FixedDelaySeconds.Should().Be(60);
        options.EnableJitter.Should().BeFalse();
        options.JitterPercentage.Should().Be(10);
        options.RequireApprovalForHighRisk.Should().BeTrue();
        options.RequireApprovalForCriticalRisk.Should().BeTrue();
        options.AllowImmediateRetryForLowRisk.Should().BeFalse();
    }

    [Theory]
    [InlineData(429, RetryAgentReasonCode.RateLimited)]
    [InlineData(408, RetryAgentReasonCode.Timeout)]
    [InlineData(504, RetryAgentReasonCode.Timeout)]
    [InlineData(500, RetryAgentReasonCode.ServerError)]
    public async Task RetryableStatusCodes_ReturnExponentialBackoff(int statusCode, RetryAgentReasonCode reasonCode)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(statusCode));
        response.RetryDecision.Should().Be(RetryAgentDecision.RetryWithExponentialBackoff);
        response.ReasonCodes.Should().Contain(reasonCode);
        response.RetryDecision.Should().NotBe(RetryAgentDecision.RetryImmediately);
    }

    [Theory]
    [InlineData(401, RetryAgentReasonCode.AuthenticationFailure)]
    [InlineData(403, RetryAgentReasonCode.AuthorizationFailure)]
    public async Task AuthFailures_RequireManualReview(int statusCode, RetryAgentReasonCode reasonCode)
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(statusCode));
        response.RetryDecision.Should().Be(RetryAgentDecision.RequireManualReview);
        response.RequiresApproval.Should().BeTrue();
        response.ReasonCodes.Should().Contain(reasonCode);
    }

    [Fact]
    public async Task NotFound_MovesToDeadLetter()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(404));
        response.RetryDecision.Should().Be(RetryAgentDecision.MoveToDeadLetter);
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.NotFound);
    }

    [Fact]
    public async Task MaxRetryReached_MovesToDeadLetterAndNeverImmediatelyRetries()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(500, retryCount: 3, maxRetryCount: 3));
        response.RetryDecision.Should().Be(RetryAgentDecision.MoveToDeadLetter);
        response.RetryDecision.Should().NotBe(RetryAgentDecision.RetryImmediately);
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.MaxRetryReached);
    }

    [Fact]
    public async Task HighRisk_RequiresApproval()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(500, riskLevel: "High"));
        response.RequiresApproval.Should().BeTrue();
        response.RiskLevel.Should().Be("High");
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.EndpointHighRisk);
    }

    [Fact]
    public async Task CriticalRisk_PausesEndpointAndRequiresApproval()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(500, riskLevel: "Critical"));
        response.RetryDecision.Should().Be(RetryAgentDecision.PauseEndpoint);
        response.RequiresApproval.Should().BeTrue();
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.EndpointCriticalRisk);
    }

    [Fact]
    public async Task DelayCalculation_ExponentialBackoffCapsAtMaxDelay()
    {
        var agent = CreateAgent(new RetryAgentOptions { BaseDelaySeconds = 30, MaxDelaySeconds = 100 });
        var response = await agent.AnalyzeAsync(CreateRequest(429, retryCount: 2));
        response.RetryDelaySeconds.Should().Be(100);
    }

    [Fact]
    public void DelayCalculation_FixedDelayAndJitterAreDeterministic()
    {
        var agent = CreateAgent(new RetryAgentOptions { FixedDelaySeconds = 60, EnableJitter = true, JitterPercentage = 10 });
        agent.CalculateDelaySeconds(RetryAgentDecision.RetryWithFixedDelay, 0).Should().Be(66);
    }

    [Fact]
    public async Task GeneratedAtUtc_IsUtcAndConfidenceIsClamped()
    {
        var response = await CreateAgent().AnalyzeAsync(CreateRequest(429));
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.ConfidenceScore.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task InvalidStatusCode_ThrowsValidationException()
    {
        var act = async () => await CreateAgent().AnalyzeAsync(CreateRequest(99));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task InvalidRetryCount_ThrowsValidationException()
    {
        var act = async () => await CreateAgent().AnalyzeAsync(CreateRequest(500, retryCount: -1));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task TextSignals_PopulateReplayAndDuplicateReasonCodesWithApproval()
    {
        var request = CreateRequest(500);
        request.FailureReason = "duplicate replay signal";
        var response = await CreateAgent().AnalyzeAsync(request);
        response.RequiresApproval.Should().BeTrue();
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.DuplicateDetected);
        response.ReasonCodes.Should().Contain(RetryAgentReasonCode.ReplayDetected);
    }

    private static RetryAgent CreateAgent(RetryAgentOptions? options = null) => new(Options.Create(options ?? new RetryAgentOptions()), NullLogger<RetryAgent>.Instance);

    private static RetryAgentRequestDto CreateRequest(int? statusCode, int retryCount = 1, int maxRetryCount = 5, string riskLevel = "Medium") => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "Tenant",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        EventType = "WebhookDeliveryFailed",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        StatusCode = statusCode,
        FailureReason = "failed",
        RetryCount = retryCount,
        MaxRetryCount = maxRetryCount,
        FailedAtUtc = DateTime.UtcNow,
        EndpointRiskLevel = riskLevel
    };
}
