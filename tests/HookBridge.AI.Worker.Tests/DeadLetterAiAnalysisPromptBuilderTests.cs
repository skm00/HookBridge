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
}
