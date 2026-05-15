using System.Text.Json;
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

public sealed class AiSecurityAnalysisRequirementTests
{
    private static readonly JsonSerializerOptions EnumJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SafePayload_FallbackProducesLowNonSuspiciousResult()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), signatureFailed: false, payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));

        response.IsSuspicious.Should().BeFalse();
        response.SecurityRiskScore.Should().BeInRange(0, 20);
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
        response.SuggestedAction.Should().BeOneOf(AiSecuritySuggestedAction.Allow, AiSecuritySuggestedAction.Monitor);
    }

    [Fact]
    public async Task SignatureValidationFailure_IncreasesRiskAndPreventsAllow()
    {
        var safe = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), signatureFailed: false, payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));
        var failed = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), signatureFailed: true, payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));

        failed.SecurityRiskScore.Should().BeGreaterThan(safe.SecurityRiskScore);
        failed.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "SignatureValidationFailed");
        failed.SuggestedAction.Should().NotBe(AiSecuritySuggestedAction.Allow);
    }

    [Fact]
    public async Task AuthenticationFailure_IncreasesRiskAndMapsAtLeastMedium()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), authFailed: true, signatureFailed: false, payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));

        response.SecurityRiskScore.Should().BeGreaterThan(20);
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "AuthenticationFailed");
        response.RiskLevel.Should().BeOneOf(AiRiskLevel.Medium, AiRiskLevel.High);
    }

    [Fact]
    public async Task LargePayload_AddsSignalAndReviewRecommendation()
    {
        var response = await CreateFallbackAgent(largePayloadThreshold: 100).AnalyzeAsync(CreateRequest(payload: Fixture("large-payload.json"), signatureFailed: false, payloadSizeBytes: 10_000, userAgent: "HookBridgeTest/1.0"));

        response.SecurityRiskScore.Should().BePositive();
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "LargePayload");
        response.Recommendation.Should().MatchRegex("(?i)(manual review|size validation|review)");
    }

    [Theory]
    [InlineData("{\"comment\":\"<script>alert(1)</script>\"}", "ScriptContent")]
    [InlineData("{\"url\":\"javascript:alert(1)\"}", "ScriptContent")]
    [InlineData("{\"query\":\"DROP TABLE users\"}", "SqlInjectionPattern")]
    [InlineData("{\"query\":\"UNION SELECT password FROM users\"}", "SqlInjectionPattern")]
    [InlineData("{\"cmd\":\"cmd.exe /c whoami\"}", "CommandInjectionPattern")]
    [InlineData("{\"cmd\":\"/bin/sh -c id\"}", "CommandInjectionPattern")]
    [InlineData("{\"cmd\":\"powershell Invoke-WebRequest\"}", "CommandInjectionPattern")]
    [InlineData("{\"path\":\"../../etc/passwd\"}", "PathTraversalPattern")]
    [InlineData("{\"path\":\"..\\\\..\\\\windows\\\\win.ini\"}", "PathTraversalPattern")]
    [InlineData("{\"password\":\"secret\"}", "SecretDetected")]
    [InlineData("{\"client_secret\":\"secret\"}", "SecretDetected")]
    [InlineData("{\"access_token\":\"token\"}", "SecretDetected")]
    public async Task FallbackRules_DetectRequiredSecurityPatterns(string payload, string expectedSignal)
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: payload, signatureFailed: false, payloadSizeBytes: payload.Length, userAgent: "HookBridgeTest/1.0"));

        response.IsSuspicious.Should().BeTrue();
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == expectedSignal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-client")]
    [InlineData("curl/8.0")]
    [InlineData("sqlmap/1.7")]
    public async Task SuspiciousUserAgent_IncreasesScore(string? userAgent)
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), signatureFailed: false, payloadSizeBytes: 128, userAgent: userAgent));

        response.SecurityRiskScore.Should().BeGreaterThan(0);
        response.DetectedSecuritySignals.Should().Contain(signal => signal.SignalName == "SuspiciousUserAgent");
    }

    [Fact]
    public async Task ScoreCalculation_ClampsExtremePayloadToOneHundredAndMultipleSignalsIncreaseScore()
    {
        var oneSignal = await CreateFallbackAgent(largePayloadThreshold: 10).AnalyzeAsync(CreateRequest(payload: Fixture("safe-payload.json"), signatureFailed: false, payloadSizeBytes: 100, userAgent: "HookBridgeTest/1.0"));
        var extreme = await CreateFallbackAgent(largePayloadThreshold: 10).AnalyzeAsync(CreateRequest(payload: "{\"x\":\"<script> DROP TABLE /bin/sh ../ password base64,\"}", signatureFailed: true, authFailed: true, payloadSizeBytes: 100, userAgent: "sqlmap"));

        oneSignal.SecurityRiskScore.Should().BeInRange(0, 100);
        extreme.SecurityRiskScore.Should().Be(100);
        extreme.SecurityRiskScore.Should().BeGreaterThan(oneSignal.SecurityRiskScore);
        extreme.DetectedSecuritySignals.Should().HaveCountGreaterThan(oneSignal.DetectedSecuritySignals.Count);
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
    public void RiskLevelMapping_UsesRequiredRanges(int score, AiRiskLevel expected)
        => AiSecurityAnalysisAgent.MapRiskLevel(score).Should().Be(expected);

    [Fact]
    public void RiskLevelMapping_InsufficientDataReturnsUnknown()
        => AiSecurityAnalysisAgent.MapRiskLevel(0, insufficientData: true).Should().Be(AiRiskLevel.Unknown);

    [Theory]
    [InlineData(AiRiskLevel.Low, false, AiSecuritySuggestedAction.Allow)]
    [InlineData(AiRiskLevel.Low, true, AiSecuritySuggestedAction.Monitor)]
    [InlineData(AiRiskLevel.Medium, false, AiSecuritySuggestedAction.Monitor)]
    [InlineData(AiRiskLevel.High, false, AiSecuritySuggestedAction.RequireManualReview)]
    [InlineData(AiRiskLevel.High, true, AiSecuritySuggestedAction.Quarantine)]
    [InlineData(AiRiskLevel.Critical, false, AiSecuritySuggestedAction.Quarantine)]
    [InlineData(AiRiskLevel.Critical, true, AiSecuritySuggestedAction.Reject)]
    public void SuggestedActionMapping_UsesRiskAndAuthContext(AiRiskLevel risk, bool authFailure, AiSecuritySuggestedAction expected)
        => AiSecurityAnalysisAgent.MapSuggestedAction(risk, CreateRequest(authFailed: authFailure, signatureFailed: false)).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"eventId\":\"evt_1\",\"securityRiskScore\":1}")]
    [InlineData("{\"summary\":\"ok\",\"recommendation\":\"ok\",\"generatedAtUtc\":\"2026-05-14T10:31:00Z\",\"suggestedAction\":\"BurnItDown\"}")]
    public async Task AiResponseParsing_InvalidEmptyMissingOrUnknownActionUsesFallback(string llmText)
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success(llmText, 1));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest());

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
    }

    [Fact]
    public async Task AiResponseParsing_UnknownRiskLevelIsHandledSafely()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success("""
        {"eventId":"evt_1","isSuspicious":false,"securityRiskScore":12,"riskLevel":"Impossible","summary":"No visible issue.","recommendation":"Monitor normally.","detectedSecuritySignals":[],"suggestedAction":"Allow","confidenceScore":0.5,"generatedAtUtc":"2026-05-14T10:31:00Z"}
        """, 1));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(signatureFailed: false, payload: Fixture("safe-payload.json"), payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));

        response.Fallback!.UsedFallback.Should().BeFalse();
        response.RiskLevel.Should().Be(AiRiskLevel.Low);
    }

    [Fact]
    public async Task AiDisabled_UsesFallbackWithProviderAndModelMetadata()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest());

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        response.Provider.Should().Be("Ollama");
        response.Model.Should().Be("llama3");
    }

    [Theory]
    [InlineData(AiFallbackReason.ProviderUnavailable)]
    [InlineData(AiFallbackReason.Timeout)]
    [InlineData(AiFallbackReason.InvalidJson)]
    [InlineData(AiFallbackReason.InvalidResponse)]
    public async Task LlmUnavailableResults_UseFallback(AiFallbackReason reason)
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Failure(reason, "llm unavailable", 10));

        var response = await CreateAgent(llm.Object).AnalyzeAsync(CreateRequest(signatureFailed: false, payload: Fixture("safe-payload.json"), payloadSizeBytes: 128, userAgent: "HookBridgeTest/1.0"));

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(reason);
    }

    [Fact]
    public void DtosAndEnums_SerializeAndDefaultSafely()
    {
        var request = new AiSecurityAnalysisRequestDto { EventId = "evt", ReceivedAtUtc = DateTime.UtcNow };
        var response = new AiSecurityAnalysisResponseDto { EventId = request.EventId, SecurityRiskScore = 5, ConfidenceScore = 0.5 };
        var signal = new AiSecuritySignalDto { SignalName = "SignatureValidationFailed", Severity = "High" };

        request.EventId.Should().Be("evt");
        response.DetectedSecuritySignals.Should().BeEmpty();
        signal.SignalName.Should().Be("SignatureValidationFailed");
        JsonSerializer.Serialize(AiSecuritySuggestedAction.RequireManualReview, EnumJsonOptions).Should().Contain("RequireManualReview");
    }

    private static AiSecurityAnalysisAgent CreateFallbackAgent(long largePayloadThreshold = 1048576)
        => CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false, largePayloadThreshold: largePayloadThreshold);

    private static AiSecurityAnalysisAgent CreateAgent(ILocalLlmClient llm, bool enabled = true, long largePayloadThreshold = 1048576)
    {
        var options = Options.Create(new AiOptions { Enabled = enabled, LargePayloadThresholdBytes = largePayloadThreshold, MaxSecurityPayloadLength = 4000, EnableSecurityAnalysisFallback = true, Provider = "Ollama", Model = "llama3" });
        return new AiSecurityAnalysisAgent(options, new AiSecurityAnalysisPromptBuilder(options), llm, NullLogger<AiSecurityAnalysisAgent>.Instance);
    }

    private static AiSecurityAnalysisRequestDto CreateRequest(string payload = "{\"comment\":\"<script>alert('x')</script>\"}", bool signatureFailed = true, bool authFailed = false, long payloadSizeBytes = 2048, string? userAgent = "unknown-client") => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "tenant",
        SubscriptionId = "sub_1",
        EndpointId = "end_1",
        Environment = "qa",
        Source = "HookBridge.API",
        EventType = "OrderCreated",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        Payload = payload,
        UserAgent = userAgent,
        SignatureValidationFailed = signatureFailed,
        AuthenticationFailed = authFailed,
        PayloadSizeBytes = payloadSizeBytes,
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "SecurityAnalysis", name));
}

public sealed class AiSecurityAnalysisPromptBuilderRequirementTests
{
    [Fact]
    public void BuildPrompt_IncludesSecurityInstructionsAllowedValuesAndContextIds()
    {
        var prompt = CreateBuilder().BuildPrompt(CreateRequest());

        prompt.Should().Contain("evt_prompt");
        prompt.Should().Contain("corr_prompt");
        prompt.Should().Contain("security analysis");
        prompt.Should().Contain("Return strict JSON only");
        prompt.Should().Contain("do not invent missing evidence");
        prompt.Should().Contain("None, Allow, Monitor, RequireManualReview, Quarantine, BlockTemporarily, Reject");
        prompt.Should().Contain("Unknown, Low, Medium, High, Critical");
    }

    [Fact]
    public void BuildPrompt_MasksHeadersAndPayloadSecrets()
    {
        var rawValues = new[] { "Bearer raw-auth-token", "cookie-secret", "api-key-secret", "access-token-secret", "client-secret-value", "password-value", "Server=.;Password=db-secret;" };
        var request = CreateRequest("""
        {"access_token":"access-token-secret","client_secret":"client-secret-value","password":"password-value","ConnectionString":"Server=.;Password=db-secret;"}
        """);
        request.Headers = new Dictionary<string, string>
        {
            ["Authorization"] = rawValues[0],
            ["Cookie"] = rawValues[1],
            ["X-API-Key"] = rawValues[2]
        };

        var prompt = CreateBuilder().BuildPrompt(request);

        prompt.Should().Contain(AiSecurityAnalysisPromptBuilder.MaskedValue);
        foreach (var raw in rawValues)
        {
            prompt.Should().NotContain(raw);
        }
    }

    [Fact]
    public void BuildPrompt_TruncatesLargePayloadAndKeepsJsonContextUsable()
    {
        var prompt = CreateBuilder(maxSecurityPayloadLength: 64).BuildPrompt(CreateRequest("{\"data\":\"" + new string('a', 1000) + "\"}"));

        prompt.Should().Contain("truncated from");
        prompt.Should().Contain("Webhook security context:");
        prompt.Should().Contain("Return strict JSON only");
    }

    [Fact]
    public async Task BuildPromptWithMetadataAsync_ReturnsSecurityPromptMetadata()
    {
        var result = await CreateBuilder().BuildPromptWithMetadataAsync(CreateRequest());

        result.Metadata.PromptName.Should().Be(HookBridge.AI.Worker.PromptVersioning.AiPromptNames.AiSecurityAnalysis);
        result.Content.Should().Contain("evt_prompt");
    }

    private static AiSecurityAnalysisPromptBuilder CreateBuilder(int maxSecurityPayloadLength = 4000)
        => new(Options.Create(new AiOptions { MaskSensitiveValues = true, MaxSecurityPayloadLength = maxSecurityPayloadLength }));

    private static AiSecurityAnalysisRequestDto CreateRequest(string payload = "{\"ok\":true}") => new()
    {
        EventId = "evt_prompt",
        CorrelationId = "corr_prompt",
        EventType = "OrderCreated",
        TargetUrl = "https://customer.example.com/webhook",
        Payload = payload,
        Headers = new Dictionary<string, string> { ["X-Test"] = "value" },
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };
}

public sealed class AiSecurityAnalysisValidationAndDiTests
{
    [Theory]
    [InlineData("", "EventId is required")]
    [InlineData("evt", "ReceivedAtUtc must be a UTC DateTime", "local-date")]
    [InlineData("evt", "TargetUrl must be a valid HTTP or HTTPS URL", "bad-url")]
    public async Task RequestValidation_RejectsInvalidRequiredFields(string eventId, string expected, string scenario = "missing-event")
    {
        var request = CreateRequest();
        request.EventId = eventId;
        if (scenario == "local-date") request.ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Local);
        if (scenario == "bad-url") request.TargetUrl = "ftp://example.com/hook";

        Func<Task> act = () => CreateAgent(enabled: false).AnalyzeAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*{expected}*");
    }

    [Fact]
    public async Task ResponseNormalization_ClampsScoresAndUtcGeneratedAt()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success("""
        {"eventId":"evt_validation","isSuspicious":false,"securityRiskScore":150,"riskLevel":"Unknown","summary":"ok","recommendation":"monitor","detectedSecuritySignals":[],"suggestedAction":"None","confidenceScore":1.7,"generatedAtUtc":"2026-05-14T10:31:00"}
        """, 1));

        var response = await CreateAgent(enabled: true, llm.Object).AnalyzeAsync(CreateRequest());

        response.SecurityRiskScore.Should().BeInRange(0, 100).And.Be(100);
        response.ConfidenceScore.Should().BeInRange(0, 1).And.Be(1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AddAiServices_ResolvesSecurityAgentPromptBuilderRepositoryAndOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Enabled"] = "false",
            ["AiMongo:ConnectionString"] = "mongodb://localhost:27017",
            ["AiMongo:DatabaseName"] = "hookbridge_tests",
            ["AiKafka:BootstrapServers"] = "localhost:9092",
            ["AiKafka:SecurityProtocol"] = "Plaintext",
            ["AiKafka:ConsumerGroupId"] = "tests"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(configuration);
        services.AddAiMongoOptions(configuration);
        services.AddAiKafkaOptions(configuration);
        services.AddAiPromptServices();
        services.AddAiSecurityAnalysisServices();
        services.AddAiMongoServices();
        services.AddSingleton(Mock.Of<ILocalLlmClient>());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAiSecurityAnalysisAgent>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisPromptBuilder>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisRepository>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<AiOptions>>().Value.Should().NotBeNull();
        provider.GetRequiredService<IOptions<AiMongoOptions>>().Value.AiSecurityAnalysisResultsCollectionName.Should().Be(AiMongoOptions.DefaultAiSecurityAnalysisResultsCollectionName);
    }

    private static AiSecurityAnalysisAgent CreateAgent(bool enabled, ILocalLlmClient? llm = null)
    {
        var options = Options.Create(new AiOptions { Enabled = enabled, EnableSecurityAnalysisFallback = true, MaxSecurityPayloadLength = 4000 });
        return new AiSecurityAnalysisAgent(options, new AiSecurityAnalysisPromptBuilder(options), llm ?? Mock.Of<ILocalLlmClient>(), NullLogger<AiSecurityAnalysisAgent>.Instance);
    }

    private static AiSecurityAnalysisRequestDto CreateRequest() => new()
    {
        EventId = "evt_validation",
        TargetUrl = "https://customer.example.com/webhook",
        Payload = "{\"ok\":true}",
        PayloadSizeBytes = 12,
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };
}
