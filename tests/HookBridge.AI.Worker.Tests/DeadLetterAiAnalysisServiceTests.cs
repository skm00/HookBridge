using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;
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
    public async Task AnalyzeAsync_InvalidAiJsonUsesFallback()
    {
        var response = await CreateService(enableAi: true, llm: LlmResponseResult.Success("not json", 1)).AnalyzeAsync(CreateRequest(429));

        Assert.True(response.Fallback.UsedFallback);
        Assert.Equal(AiFallbackReason.InvalidJson, response.Fallback.FallbackReason);
    }

    private static DeadLetterAiAnalysisService CreateService(bool enableAi, LlmResponseResult? llm = null)
    {
        var options = Options.Create(new DeadLetterAiAnalysisOptions { EnableAiAnalysis = enableAi });
        var aiOptions = Options.Create(new AiOptions { Enabled = enableAi, Provider = "test", Model = "test-model", Endpoint = "http://localhost" });
        var promptBuilder = new DeadLetterAiAnalysisPromptBuilder(options);
        return new DeadLetterAiAnalysisService(options, aiOptions, promptBuilder, NullLogger<DeadLetterAiAnalysisService>.Instance, llm is null ? null : new FakeLlmClient(llm));
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

    private sealed class FakeLlmClient(LlmResponseResult result) : ILocalLlmClient
    {
        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
