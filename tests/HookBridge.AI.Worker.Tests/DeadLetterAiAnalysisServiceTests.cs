using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;
using HookBridge.AI.Worker.Services.SafeMode;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class DeadLetterAiAnalysisServiceTests
{
    [Theory]
    [InlineData(429, DeadLetterSuggestedAction.ReplayWithBackoff, DeadLetterReplaySafety.ReplayWithCaution, DeadLetterReasonCode.RateLimited)]
    [InlineData(500, DeadLetterSuggestedAction.ReplayWithBackoff, DeadLetterReplaySafety.ReplayWithCaution, DeadLetterReasonCode.ServerError)]
    [InlineData(401, DeadLetterSuggestedAction.FixAuthenticationBeforeReplay, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterReasonCode.AuthenticationFailure)]
    [InlineData(403, DeadLetterSuggestedAction.FixAuthenticationBeforeReplay, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterReasonCode.AuthorizationFailure)]
    [InlineData(400, DeadLetterSuggestedAction.FixPayloadBeforeReplay, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterReasonCode.PayloadContractIssue)]
    [InlineData(404, DeadLetterSuggestedAction.FixEndpointBeforeReplay, DeadLetterReplaySafety.RequiresFixBeforeReplay, DeadLetterReasonCode.NotFound)]
    public async Task AnalyzeAsync_MapsHttpFallbackRules(int statusCode, DeadLetterSuggestedAction action, DeadLetterReplaySafety safety, DeadLetterReasonCode reason)
    {
        var response = await CreateService(enableAi: false).AnalyzeAsync(CreateRequest(statusCode));

        Assert.Equal(action, response.SuggestedAction);
        Assert.Equal(safety, response.ReplaySafety);
        Assert.Contains(reason, response.ReasonCodes);
        Assert.Contains(DeadLetterReasonCode.MaxRetryReached, response.ReasonCodes);
        Assert.Equal(DateTimeKind.Utc, response.GeneratedAtUtc.Kind);
    }

    [Fact]
    public async Task AnalyzeAsync_SuspiciousReplayAndDuplicateAreNotReplayable()
    {
        var service = CreateService(enableAi: false);

        var suspicious = await service.AnalyzeAsync(CreateRequest(500, isSuspicious: true));
        var replay = await service.AnalyzeAsync(CreateRequest(500, isReplay: true));
        var duplicate = await service.AnalyzeAsync(CreateRequest(500, isDuplicate: true));

        Assert.Equal(DeadLetterReplaySafety.DoNotReplay, suspicious.ReplaySafety);
        Assert.Equal(DeadLetterSuggestedAction.Quarantine, suspicious.SuggestedAction);
        Assert.Equal(DeadLetterReplaySafety.DoNotReplay, replay.ReplaySafety);
        Assert.Equal(DeadLetterSuggestedAction.KeepInDeadLetter, duplicate.SuggestedAction);
    }


    [Theory]
    [InlineData(408, DeadLetterReasonCode.Timeout)]
    [InlineData(504, DeadLetterReasonCode.Timeout)]
    [InlineData(502, DeadLetterReasonCode.ServerError)]
    [InlineData(503, DeadLetterReasonCode.ServerError)]
    [InlineData(409, DeadLetterReasonCode.ClientError)]
    public async Task AnalyzeAsync_AdditionalHttpRulesAreDeterministic(int statusCode, DeadLetterReasonCode reason)
    {
        var response = await CreateService(enableAi: false).AnalyzeAsync(CreateRequest(statusCode));

        Assert.Contains(reason, response.ReasonCodes);
        Assert.NotEqual(DeadLetterReplaySafety.Unknown, response.ReplaySafety);
        Assert.False(response.IsActionAllowed);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotAddMaxRetryReason_WhenRetryBudgetRemains()
    {
        var response = await CreateService(enableAi: false).AnalyzeAsync(CreateRequest(429, retryCount: 1, maxRetryCount: 5));

        Assert.DoesNotContain(DeadLetterReasonCode.MaxRetryReached, response.ReasonCodes);
        Assert.Equal(DeadLetterSuggestedAction.ReplayWithBackoff, response.SuggestedAction);
    }

    [Fact]
    public async Task AnalyzeAsync_LlmUnavailableUsesProviderFallback()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "offline", 1)).AnalyzeAsync(CreateRequest(500));

        Assert.True(response.Fallback.UsedFallback);
        Assert.Equal(AiFallbackReason.ProviderUnavailable, response.Fallback.FallbackReason);
        Assert.Equal(DeadLetterSuggestedAction.ReplayWithBackoff, response.SuggestedAction);
    }

    [Fact]
    public async Task AnalyzeAsync_AiDirectReplayForAuthenticationFailureIsOverridden()
    {
        var json = """
        {
          "deadLetterId":"dlq_1",
          "eventId":"evt_1",
          "summary":"AI summary",
          "rootCause":"AI root cause",
          "recommendation":"AI recommendation",
          "replaySafety":"SafeToReplay",
          "suggestedAction":"Replay",
          "riskLevel":"Low",
          "confidenceScore": 0.8,
          "confidenceLevel":"High",
          "requiresApproval": false,
          "reasonCodes":[],
          "generatedAtUtc":"2026-05-14T10:46:00Z"
        }
        """;

        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(json, 1)).AnalyzeAsync(CreateRequest(401));

        Assert.Equal(DeadLetterSuggestedAction.FixAuthenticationBeforeReplay, response.SuggestedAction);
        Assert.True(response.RequiresApproval);
        Assert.Contains(DeadLetterReasonCode.MaxRetryReached, response.ReasonCodes);
    }

    [Fact]
    public async Task AnalyzeAsync_AiDirectReplayForPayloadContractIssueIsOverridden()
    {
        var json = """
        {
          "deadLetterId":"dlq_1",
          "eventId":"evt_1",
          "summary":"AI summary",
          "rootCause":"AI root cause",
          "recommendation":"AI recommendation",
          "replaySafety":"SafeToReplay",
          "suggestedAction":"Replay",
          "riskLevel":"Low",
          "confidenceScore": 0.8,
          "confidenceLevel":"High",
          "requiresApproval": false,
          "reasonCodes":[],
          "generatedAtUtc":"2026-05-14T10:46:00Z"
        }
        """;

        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(json, 1)).AnalyzeAsync(CreateRequest(400));

        Assert.Equal(DeadLetterSuggestedAction.FixPayloadBeforeReplay, response.SuggestedAction);
        Assert.True(response.RequiresApproval);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidRequestThrowsValidationException()
    {
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => CreateService(enableAi: false).AnalyzeAsync(new DeadLetterAiAnalysisRequestDto
        {
            EventId = "evt_1",
            RetryCount = -1,
            FailedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
            MovedToDeadLetterAtUtc = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task AnalyzeAsync_UnknownStatusRequiresManualReview()
    {
        var response = await CreateService(enableAi: false).AnalyzeAsync(CreateRequest(null));

        Assert.Equal(DeadLetterSuggestedAction.RequireManualReview, response.SuggestedAction);
        Assert.Equal(DeadLetterReplaySafety.RequiresManualReview, response.ReplaySafety);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidAiJsonParsesAndClampsConfidence()
    {
        var json = """
        {
          "deadLetterId":"dlq_1",
          "eventId":"evt_1",
          "summary":"AI summary",
          "rootCause":"AI root cause",
          "recommendation":"AI recommendation",
          "replaySafety":"ReplayWithCaution",
          "suggestedAction":"ReplayWithBackoff",
          "riskLevel":"Medium",
          "confidenceScore": 2,
          "confidenceLevel":"High",
          "requiresApproval": false,
          "reasonCodes":["RateLimited"],
          "generatedAtUtc":"2026-05-14T10:46:00Z"
        }
        """;

        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(json, 1)).AnalyzeAsync(CreateRequest(429));

        Assert.Equal("AI summary", response.Summary);
        Assert.Equal(1, response.ConfidenceScore);
        Assert.True(response.RequiresApproval);
        Assert.False(response.Fallback.UsedFallback);
    }


    [Fact]
    public async Task AnalyzeAsync_LlmExceptionUsesFallback()
    {
        var response = await CreateService(enableAi: true, throwingLlm: true).AnalyzeAsync(CreateRequest(500));

        Assert.True(response.Fallback.UsedFallback);
        Assert.Equal(AiFallbackReason.ProviderUnavailable, response.Fallback.FallbackReason);
        Assert.Equal(DeadLetterReplaySafety.ReplayWithCaution, response.ReplaySafety);
    }

    [Fact]
    public async Task AnalyzeAsync_AiResponseMissingRequiredFieldsUsesFallback()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success("{}", 1)).AnalyzeAsync(CreateRequest(404));

        Assert.True(response.Fallback.UsedFallback);
        Assert.Equal(AiFallbackReason.InvalidJson, response.Fallback.FallbackReason);
        Assert.Equal(DeadLetterSuggestedAction.FixEndpointBeforeReplay, response.SuggestedAction);
    }

    [Fact]
    public async Task AnalyzeAsync_AiResponseMissingIdsAndGeneratedDateUsesRequestContextAndUtc()
    {
        var json = """
        {
          "summary":"AI summary",
          "rootCause":"AI root cause",
          "recommendation":"AI recommendation",
          "replaySafety":"RequiresManualReview",
          "suggestedAction":"RequireManualReview",
          "riskLevel":"Low",
          "confidenceScore": 0.4,
          "reasonCodes":[]
        }
        """;

        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(json, 1)).AnalyzeAsync(CreateRequest(null));

        Assert.Equal("dlq_1", response.DeadLetterId);
        Assert.Equal("evt_1", response.EventId);
        Assert.Equal("corr_1", response.CorrelationId);
        Assert.Equal(AiConfidenceLevel.Low, response.ConfidenceLevel);
        Assert.Equal(DateTimeKind.Utc, response.GeneratedAtUtc.Kind);
        Assert.False(response.Fallback.UsedFallback);
    }

    [Fact]
    public async Task AnalyzeAsync_AiSafeToReplayForSuspiciousEventIsBlocked()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(SafeReplayJson(), 1)).AnalyzeAsync(CreateRequest(500, isSuspicious: true));

        Assert.Equal(DeadLetterReplaySafety.DoNotReplay, response.ReplaySafety);
        Assert.Equal("Critical", response.RiskLevel);
        Assert.Contains(DeadLetterReasonCode.SuspiciousPayload, response.ReasonCodes);
        Assert.True(response.RequiresApproval);
    }

    [Fact]
    public async Task AnalyzeAsync_AiSafeToReplayForReplayEventIsBlocked()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success(SafeReplayJson(), 1)).AnalyzeAsync(CreateRequest(500, isReplay: true));

        Assert.Equal(DeadLetterReplaySafety.DoNotReplay, response.ReplaySafety);
        Assert.Equal("High", response.RiskLevel);
        Assert.Contains(DeadLetterReasonCode.ReplayDetected, response.ReasonCodes);
        Assert.True(response.RequiresApproval);
    }

    [Fact]
    public async Task AnalyzeAsync_SafeModeAllowedCanAllowReadOnlyManualReview()
    {
        var safeMode = new FakeSafeModeGuard(new AiSafeModeEvaluationResponseDto
        {
            Decision = AiSafeModeDecision.Allowed,
            IsAllowed = true,
            RequiresApproval = false,
            ActionType = AiActionType.ReadOnlyQuery,
            Environment = "qa"
        });

        var response = await CreateService(enableAi: false, safeModeGuard: safeMode).AnalyzeAsync(CreateRequest(null, retryCount: 0, maxRetryCount: 5));

        Assert.Equal(AiSafeModeDecision.Allowed, response.SafeModeDecision);
        Assert.True(response.IsActionAllowed);
        Assert.False(response.RequiresApproval);
        Assert.Equal(AiActionType.ReadOnlyQuery, safeMode.LastRequest!.ActionType);
    }

    [Fact]
    public async Task AnalyzeAsync_SafeModeRequiresApprovalKeepsReplayBlocked()
    {
        var safeMode = new FakeSafeModeGuard(new AiSafeModeEvaluationResponseDto
        {
            Decision = AiSafeModeDecision.RequiresApproval,
            IsAllowed = false,
            RequiresApproval = true,
            ActionType = AiActionType.ReplayDeadLetter,
            Environment = "production"
        });

        var response = await CreateService(enableAi: false, safeModeGuard: safeMode).AnalyzeAsync(CreateRequest(429));

        Assert.Equal(AiSafeModeDecision.RequiresApproval, response.SafeModeDecision);
        Assert.True(response.RequiresApproval);
        Assert.False(response.IsActionAllowed);
        Assert.Equal(AiActionType.ReplayDeadLetter, safeMode.LastRequest!.ActionType);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidAiJsonUsesFallback()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success("not json", 1)).AnalyzeAsync(CreateRequest(429));

        Assert.True(response.Fallback.UsedFallback);
        Assert.Equal(AiFallbackReason.InvalidJson, response.Fallback.FallbackReason);
    }

    private static DeadLetterAiAnalysisService CreateService(bool enableAi, LlmResponseResult? llm = null, bool throwingLlm = false, IAiSafeModeGuard? safeModeGuard = null)
    {
        var options = Options.Create(new DeadLetterAiAnalysisOptions { EnableAiAnalysis = enableAi });
        var aiOptions = Options.Create(new AiOptions { Enabled = enableAi, Provider = "test", Model = "test-model", Endpoint = "http://localhost" });
        var promptBuilder = new DeadLetterAiAnalysisPromptBuilder(options);
        ILocalLlmClient? llmClient = throwingLlm ? new ThrowingLlmClient() : llm is null ? null : new FakeLlmClient(llm);
        return new DeadLetterAiAnalysisService(options, aiOptions, promptBuilder, NullLogger<DeadLetterAiAnalysisService>.Instance, llmClient, safeModeGuard);
    }

    private static DeadLetterAiAnalysisRequestDto CreateRequest(int? statusCode, bool isSuspicious = false, bool isReplay = false, bool isDuplicate = false, int retryCount = 5, int maxRetryCount = 5) => new()
    {
        DeadLetterId = "dlq_1",
        EventId = "evt_1",
        CorrelationId = "corr_1",
        StatusCode = statusCode,
        RetryCount = retryCount,
        MaxRetryCount = maxRetryCount,
        FailedAtUtc = DateTime.UtcNow,
        MovedToDeadLetterAtUtc = DateTime.UtcNow,
        IsSuspicious = isSuspicious,
        IsReplay = isReplay,
        IsDuplicate = isDuplicate
    };



    private static string SafeReplayJson() => """
        {
          "deadLetterId":"dlq_1",
          "eventId":"evt_1",
          "summary":"AI summary",
          "rootCause":"AI root cause",
          "recommendation":"AI recommendation",
          "replaySafety":"SafeToReplay",
          "suggestedAction":"Replay",
          "riskLevel":"Low",
          "confidenceScore": 0.8,
          "confidenceLevel":"High",
          "requiresApproval": false,
          "reasonCodes":[],
          "generatedAtUtc":"2026-05-14T10:46:00Z"
        }
        """;

    private sealed class FakeLlmClient(LlmResponseResult result) : ILocalLlmClient
    {
        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class ThrowingLlmClient : ILocalLlmClient
    {
        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default) => throw new InvalidOperationException("LLM failed.");
    }

    private sealed class FakeSafeModeGuard(AiSafeModeEvaluationResponseDto response) : IAiSafeModeGuard
    {
        public AiSafeModeEvaluationRequestDto? LastRequest { get; private set; }

        public Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
