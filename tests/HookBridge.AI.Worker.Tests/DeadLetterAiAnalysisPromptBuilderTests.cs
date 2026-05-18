using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Prompts;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class DeadLetterAiAnalysisPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_MasksSensitiveValuesAndTruncatesPayload()
    {
        var builder = new DeadLetterAiAnalysisPromptBuilder(Options.Create(new DeadLetterAiAnalysisOptions { MaxPayloadLength = 20, MaxResponseBodyLength = 20 }));

        var prompt = builder.BuildPrompt(new DeadLetterAiAnalysisRequestDto
        {
            DeadLetterId = "dlq_1",
            EventId = "evt_1",
            FailedAtUtc = DateTime.UtcNow,
            MovedToDeadLetterAtUtc = DateTime.UtcNow,
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer secret-token" },
            Payload = "{\"password\":\"super-secret\",\"data\":\"abcdefghijklmnopqrstuvwxyz\"}",
            ResponseBody = "abcdefghijklmnopqrstuvwxyz"
        });

        Assert.Contains("[MASKED]", prompt);
        Assert.DoesNotContain("super-secret", prompt);
        Assert.DoesNotContain("Bearer secret-token", prompt);
        Assert.Contains("truncated from", prompt);
        Assert.Contains("requires approval", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPromptWithMetadataAsync_ReturnsStablePromptMetadata()
    {
        var builder = new DeadLetterAiAnalysisPromptBuilder(Options.Create(new DeadLetterAiAnalysisOptions()));

        var result = await builder.BuildPromptWithMetadataAsync(new DeadLetterAiAnalysisRequestDto
        {
            DeadLetterId = "dlq_1",
            EventId = "evt_1",
            FailedAtUtc = DateTime.UtcNow,
            MovedToDeadLetterAtUtc = DateTime.UtcNow
        });

        Assert.Equal(DeadLetterAiAnalysisPromptBuilder.PromptName, result.Metadata.PromptName);
        Assert.Equal(DeadLetterAiAnalysisPromptBuilder.PromptVersion, result.Metadata.Version);
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata.Hash));
        Assert.Contains("strict JSON", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Options_DefaultsMatchApprovalAndTruncationPolicy()
    {
        var options = new DeadLetterAiAnalysisOptions();

        Assert.True(options.Enabled);
        Assert.True(options.EnableAiAnalysis);
        Assert.Equal(4000, options.MaxPayloadLength);
        Assert.Equal(2000, options.MaxResponseBodyLength);
        Assert.True(options.RequireApprovalForReplay);
        Assert.True(options.RequireApprovalForHighRisk);
        Assert.True(options.RequireApprovalForCriticalRisk);
        Assert.True(options.RequireApprovalForSuspiciousEvents);
    }

}
