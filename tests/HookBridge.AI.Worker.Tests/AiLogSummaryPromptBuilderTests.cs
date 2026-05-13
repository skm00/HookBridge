using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiLogSummaryPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_RequestsStrictJsonOutput()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("Return strict JSON only");
        prompt.Should().Contain("\"summary\"");
        prompt.Should().Contain("\"rootCause\"");
        prompt.Should().Contain("\"impact\"");
        prompt.Should().Contain("\"recommendation\"");
        prompt.Should().Contain("\"riskLevel\"");
        prompt.Should().Contain("\"confidenceScore\"");
        prompt.Should().Contain("\"generatedAtUtc\"");
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValues()
    {
        var request = CreateRequest();
        request.Logs =
        [
            new AiLogEntryDto
            {
                TimestampUtc = DateTime.UnixEpoch,
                Level = "Error",
                Message = "Authorization: Bearer secret-token Cookie=session-secret Token=token-secret Secret=super-secret Password=pwd Api-Key=api-secret X-API-Key=x-secret ConnectionString=Server=prod;Database=hookbridge",
                Exception = "Set-Cookie: session=secret"
            }
        ];

        var prompt = CreateBuilder().BuildPrompt(request);

        prompt.Should().Contain("[MASKED]");
        prompt.Should().NotContain("secret-token");
        prompt.Should().NotContain("session-secret");
        prompt.Should().NotContain("token-secret");
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("pwd");
        prompt.Should().NotContain("api-secret");
        prompt.Should().NotContain("x-secret");
        prompt.Should().NotContain("Server=prod");
    }

    [Fact]
    public void BuildPrompt_TruncatesLargeLogMessages()
    {
        var request = CreateRequest();
        request.Logs =
        [
            new AiLogEntryDto
            {
                TimestampUtc = DateTime.UnixEpoch,
                Level = "Error",
                Message = new string('a', 25)
            }
        ];

        var prompt = CreateBuilder(maxLogMessageLength: 10).BuildPrompt(request);

        prompt.Should().Contain("aaaaaaaaaa... [truncated from 25 to 10 characters]");
        prompt.Should().NotContain(new string('a', 25));
    }

    [Fact]
    public void BuildPrompt_LimitsTooManyLogs()
    {
        var request = CreateRequest();
        request.Logs = Enumerable.Range(1, 5)
            .Select(i => new AiLogEntryDto
            {
                TimestampUtc = DateTime.UnixEpoch.AddMinutes(i),
                Level = "Information",
                Message = $"log-{i}"
            })
            .ToArray();

        var prompt = CreateBuilder(maxLogEntriesForSummary: 3).BuildPrompt(request);

        prompt.Should().Contain("\"includedLogCount\": 3");
        prompt.Should().Contain("\"omittedLogCount\": 2");
        prompt.Should().Contain("log-1");
        prompt.Should().Contain("log-3");
        prompt.Should().NotContain("log-4");
        prompt.Should().NotContain("log-5");
    }

    [Fact]
    public void AddAiPromptServices_RegistersLogSummaryPromptBuilder()
    {
        var services = new ServiceCollection();
        services.AddAiOptions(new ConfigurationBuilder().Build());
        services.AddAiPromptServices();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAiLogSummaryPromptBuilder>()
            .Should().BeOfType<AiLogSummaryPromptBuilder>();
    }

    private static AiLogSummaryPromptBuilder CreateBuilder(
        int maxLogEntriesForSummary = 100,
        int maxLogMessageLength = 2000,
        bool maskSensitiveValues = true)
        => new(Options.Create(new AiOptions
        {
            MaxLogEntriesForSummary = maxLogEntriesForSummary,
            MaxLogMessageLength = maxLogMessageLength,
            MaskSensitiveValues = maskSensitiveValues
        }));

    private static AiLogSummaryRequestDto CreateRequest()
        => new()
        {
            EventId = "evt_123",
            CorrelationId = "corr_123",
            Source = "hookbridge.worker",
            Environment = "qa",
            FromUtc = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 5, 13, 10, 15, 0, DateTimeKind.Utc),
            Logs =
            [
                new AiLogEntryDto
                {
                    TimestampUtc = new DateTime(2026, 5, 13, 10, 10, 0, DateTimeKind.Utc),
                    Level = "Error",
                    Message = "Webhook delivery failed with HTTP 429 Too Many Requests",
                    ServiceName = "HookBridge.Worker",
                    TraceId = "trace-1",
                    SpanId = "span-1"
                }
            ]
        };
}
