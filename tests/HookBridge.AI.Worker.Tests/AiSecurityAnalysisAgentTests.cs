using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSecurityAnalysisAgentTests
{
    [Fact]
    public async Task AnalyzeAsync_ParsesValidAiResponse()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"eventId":"evt_1","correlationId":"corr_1","isSuspicious":true,"securityRiskScore":72,"riskLevel":"High","summary":"Suspicious signature and script content.","recommendation":"Quarantine and review.","detectedSecuritySignals":[{"signalName":"SignatureValidationFailed","severity":"High","description":"Signature failed.","evidence":"signatureValidationFailed=true","recommendation":"Verify secret."}],"suggestedAction":"Quarantine","confidenceScore":0.78,"generatedAtUtc":"2026-05-14T10:31:00Z"}
            """, 5));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest());

        response.SecurityRiskScore.Should().Be(72);
        response.RiskLevel.Should().Be(AiRiskLevel.High);
        response.SuggestedAction.Should().Be(AiSecuritySuggestedAction.Quarantine);
        response.Fallback!.UsedFallback.Should().BeFalse();
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("{\"comment\":\"<script>alert(1)</script>\"}", "ScriptContent")]
    [InlineData("{\"query\":\"DROP TABLE users\"}", "SqlInjectionPattern")]
    [InlineData("{\"cmd\":\"/bin/sh -c whoami\"}", "CommandInjectionPattern")]
    [InlineData("{\"path\":\"../../etc/passwd\"}", "PathTraversalPattern")]
    [InlineData("{\"client_secret\":\"secret\",\"access_token\":\"abc\"}", "SecretDetected")]
    public async Task AnalyzeAsync_FallbackDetectsSuspiciousPatterns(string payload, string signalName)
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).AnalyzeAsync(CreateRequest(payload: payload, signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == signalName);
        response.IsSuspicious.Should().BeTrue();
        response.Fallback!.UsedFallback.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidAiJsonUsesFallback()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success("not json", 1));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        response.SecurityRiskScore.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task AnalyzeAsync_SignatureAuthenticationAndLargePayloadIncreaseScoreAndClamp()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false, largePayloadThreshold: 10)
            .AnalyzeAsync(CreateRequest(payload: "{\"cmd\":\"powershell ../ DROP TABLE <script password\"}", authFailed: true, payloadSizeBytes: 100));

        response.SecurityRiskScore.Should().Be(100);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
        response.SuggestedAction.Should().NotBe(AiSecuritySuggestedAction.Allow);
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "AuthenticationFailed");
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "LargePayload");
    }

    [Theory]
    [InlineData(0, AiRiskLevel.Low)]
    [InlineData(21, AiRiskLevel.Medium)]
    [InlineData(51, AiRiskLevel.High)]
    [InlineData(81, AiRiskLevel.Critical)]
    public void MapRiskLevel_UsesConfiguredThresholds(int score, AiRiskLevel expected)
        => AiSecurityAnalysisAgent.MapRiskLevel(score).Should().Be(expected);


    [Fact]
    public async Task AnalyzeAsync_ProviderUnavailableUsesFallbackReason()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "ollama unavailable", 10));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        response.Provider.Should().Be("Ollama");
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidPayloadJsonUsesFallbackBeforeCallingLlm()
    {
        var llm = new Mock<ILocalLlmClient>();

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(payload: "{bad json", signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        llm.Verify(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task AnalyzeAsync_NullAiSignalsDefaultsToEmptyList()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"eventId":"evt_1","isSuspicious":false,"securityRiskScore":5,"riskLevel":"Low","summary":"ok","recommendation":"monitor","detectedSecuritySignals":null,"suggestedAction":"Allow","confidenceScore":0.5,"generatedAtUtc":"2026-05-14T10:31:00Z"}
            """, 1));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.DetectedSecuritySignals.Should().BeEmpty();
        response.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFallbackDisabledReturnsUnknownAdvisoryResponse()
    {
        var options = Options.Create(new AiOptions { Enabled = false, EnableSecurityAnalysisFallback = false });
        var agent = new AiSecurityAnalysisAgent(options, new AiSecurityAnalysisPromptBuilder(options), Mock.Of<ILocalLlmClient>(), NullLogger<AiSecurityAnalysisAgent>.Instance);

        var response = await agent.AnalyzeAsync(CreateRequest(signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.RiskLevel.Should().Be(AiRiskLevel.Unknown);
        response.SuggestedAction.Should().Be(AiSecuritySuggestedAction.Monitor);
        response.ConfidenceScore.Should().Be(0.1);
    }

    [Fact]
    public async Task AnalyzeAsync_ClampsAiRiskAndConfidenceAndDefaultsAction()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"eventId":"","isSuspicious":false,"securityRiskScore":150,"riskLevel":"Unknown","summary":"ok","recommendation":"review","detectedSecuritySignals":[],"suggestedAction":"None","confidenceScore":1.7,"generatedAtUtc":"2026-05-14T10:31:00"}
            """, 1));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(signatureFailed: false, userAgent: "HookBridgeTest/1.0"));

        response.EventId.Should().Be("evt_1");
        response.SecurityRiskScore.Should().Be(100);
        response.ConfidenceScore.Should().Be(1);
        response.RiskLevel.Should().Be(AiRiskLevel.Critical);
        response.SuggestedAction.Should().Be(AiSecuritySuggestedAction.Quarantine);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("", "EventId is required", "missing-event")]
    [InlineData("evt_1", "ReceivedAtUtc must be a UTC DateTime", "local-date")]
    [InlineData("evt_1", "PayloadSizeBytes must be greater than or equal to zero", "negative-size")]
    [InlineData("evt_1", "TargetUrl must be a valid HTTP or HTTPS URL", "bad-url")]
    public async Task AnalyzeAsync_ValidatesRequest(string eventId, string expectedMessage, string scenario = "missing-event")
    {
        var request = CreateRequest(signatureFailed: false, userAgent: "HookBridgeTest/1.0");
        request.EventId = eventId;
        if (scenario == "local-date") request.ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Local);
        if (scenario == "negative-size") request.PayloadSizeBytes = -1;
        if (scenario == "bad-url") request.TargetUrl = "ftp://example.com/hook";

        Func<Task> act = async () => await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).AnalyzeAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*{expectedMessage}*");
    }

    [Theory]
    [InlineData(AiRiskLevel.Low, false, AiSecuritySuggestedAction.Allow)]
    [InlineData(AiRiskLevel.Low, true, AiSecuritySuggestedAction.Monitor)]
    [InlineData(AiRiskLevel.Medium, false, AiSecuritySuggestedAction.Monitor)]
    [InlineData(AiRiskLevel.High, false, AiSecuritySuggestedAction.RequireManualReview)]
    [InlineData(AiRiskLevel.High, true, AiSecuritySuggestedAction.Quarantine)]
    [InlineData(AiRiskLevel.Critical, false, AiSecuritySuggestedAction.Quarantine)]
    [InlineData(AiRiskLevel.Critical, true, AiSecuritySuggestedAction.Reject)]
    [InlineData(AiRiskLevel.Unknown, false, AiSecuritySuggestedAction.Monitor)]
    public void MapSuggestedAction_UsesRiskAndAuthContext(AiRiskLevel riskLevel, bool authFailed, AiSecuritySuggestedAction expected)
    {
        var request = CreateRequest(signatureFailed: false, authFailed: authFailed, userAgent: "HookBridgeTest/1.0");

        AiSecurityAnalysisAgent.MapSuggestedAction(riskLevel, request).Should().Be(expected);
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValuesAndTruncatesPayload()
    {
        var builder = new AiSecurityAnalysisPromptBuilder(Options.Create(new AiOptions { MaxSecurityPayloadLength = 30, MaskSensitiveValues = true }));

        var prompt = builder.BuildPrompt(CreateRequest(payload: "{\"password\":\"super-secret\",\"data\":\"" + new string('a', 200) + "\"}", headers: new Dictionary<string, string> { ["Authorization"] = "Bearer abc" }));

        prompt.Should().Contain(AiSecurityAnalysisPromptBuilder.MaskedValue);
        prompt.Should().Contain("truncated from");
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("Bearer abc");
    }


    [Fact]
    public void BuildPrompt_HandlesNullPayloadAndHeaders()
    {
        var builder = new AiSecurityAnalysisPromptBuilder(Options.Create(new AiOptions { MaxSecurityPayloadLength = 4000, MaskSensitiveValues = true }));
        var request = CreateRequest(payload: "{}", headers: null, signatureFailed: false, userAgent: "HookBridgeTest/1.0");
        request.Payload = null;

        var prompt = builder.BuildPrompt(request);

        prompt.Should().Contain("\"headers\": {}");
        prompt.Should().Contain("strict JSON only");
    }

    [Fact]
    public void BuildPrompt_SerializesObjectPayloadAndMasksAssignmentStyleSecrets()
    {
        var builder = new AiSecurityAnalysisPromptBuilder(Options.Create(new AiOptions { MaxSecurityPayloadLength = 4000, MaskSensitiveValues = true }));
        var request = CreateRequest(payload: "{}", headers: new Dictionary<string, string> { ["X-Trace"] = "client_secret=abc123" }, signatureFailed: false, userAgent: "HookBridgeTest/1.0");
        request.Payload = new { password = "p@ss", nested = new { value = 5 } };

        var prompt = builder.BuildPrompt(request);

        prompt.Should().Contain(AiSecurityAnalysisPromptBuilder.MaskedValue);
        prompt.Should().NotContain("p@ss");
        prompt.Should().NotContain("abc123");
        prompt.Should().Contain("nested");
    }

    [Fact]
    public void CreateAiSecurityAnalysisIndexModels_IncludesRequiredIndexes()
    {
        var names = AiMongoIndexInitializer.CreateAiSecurityAnalysisIndexModels().Select(model => model.Options.Name).ToArray();
        names.Should().Contain("idx_ai_security_analysis_event_id");
        names.Should().Contain("idx_ai_security_analysis_generated_at_utc_desc");
        names.Should().HaveCount(9);
    }

    [Fact]
    public void AddAiServices_RegistersSecurityAnalysisServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Enabled"] = "false",
            ["AiKafka:BootstrapServers"] = "localhost:9092",
            ["AiKafka:ConsumerGroupId"] = "tests"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(configuration);
        services.AddAiPromptServices();
        services.AddAiSecurityAnalysisServices();
        services.AddSingleton(Mock.Of<ILocalLlmClient>());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAiSecurityAnalysisAgent>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisPromptBuilder>().Should().NotBeNull();
    }

    private static AiSecurityAnalysisAgent CreateAgent(ILocalLlmClient llm, bool enabled = true, long largePayloadThreshold = 1048576)
    {
        var options = Options.Create(new AiOptions { Enabled = enabled, LargePayloadThresholdBytes = largePayloadThreshold, MaxSecurityPayloadLength = 4000, EnableSecurityAnalysisFallback = true });
        return new AiSecurityAnalysisAgent(options, new AiSecurityAnalysisPromptBuilder(options), llm, NullLogger<AiSecurityAnalysisAgent>.Instance);
    }

    private static AiSecurityAnalysisRequestDto CreateRequest(string payload = "{\"comment\":\"<script>alert('x')</script>\"}", IDictionary<string, string>? headers = null, bool signatureFailed = true, bool authFailed = false, long payloadSizeBytes = 2048, string userAgent = "unknown-client") => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        SubscriptionId = "sub_1",
        EndpointId = "end_1",
        Environment = "qa",
        Source = "HookBridge.API",
        EventType = "OrderCreated",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        Headers = headers,
        Payload = payload,
        SourceIp = "10.10.10.10",
        UserAgent = userAgent,
        SignatureValidationFailed = signatureFailed,
        AuthenticationFailed = authFailed,
        PayloadSizeBytes = payloadSizeBytes,
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };
}
