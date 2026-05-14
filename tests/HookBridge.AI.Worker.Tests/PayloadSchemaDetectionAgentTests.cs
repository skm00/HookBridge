using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.PayloadSchemaDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class PayloadSchemaDetectionAgentTests
{
    [Fact]
    public async Task DetectAsync_ParsesValidAiResponse()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {
              "eventId":"evt_1",
              "correlationId":"corr_1",
              "detectedSchemaName":"OrderCreated",
              "detectedEventType":"OrderCreated",
              "summary":"Order payload.",
              "importantFields":[{"fieldName":"orderId","jsonPath":"$.orderId","inferredType":"string","isRequired":true,"sampleValue":"ORD-1","description":"Order id."}],
              "missingFields":[],
              "validationIssues":[],
              "suggestedDtoName":"OrderCreatedDto",
              "confidenceScore":0.86,
              "riskLevel":"Low",
              "generatedAtUtc":"2026-05-14T10:31:00Z"
            }
            """, 5));

        var response = await CreateAgent(llm.Object).DetectAsync(CreateRequest());

        response.DetectedSchemaName.Should().Be("OrderCreated");
        response.ImportantFields.Should().ContainSingle(field => field.FieldName == "orderId");
        response.ConfidenceScore.Should().Be(0.86);
        response.Fallback!.UsedFallback.Should().BeFalse();
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task DetectAsync_FallsBackWhenAiReturnsInvalidJson()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("not json", 5));

        var response = await CreateAgent(llm.Object).DetectAsync(CreateRequest());

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        response.ImportantFields.Should().Contain(field => field.FieldName == "orderId" && field.InferredType == "string");
    }

    [Fact]
    public async Task DetectAsync_FallsBackWhenAiIsDisabled()
    {
        var llm = new Mock<ILocalLlmClient>();

        var response = await CreateAgent(llm.Object, enabled: false).DetectAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        response.ConfidenceScore.Should().BeLessThan(0.5);
        llm.Verify(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectAsync_HandlesInvalidPayloadJsonWithFallback()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>()).DetectAsync(CreateRequest(payload: "{bad json"));

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.RiskLevel.Should().Be("Medium");
        response.ValidationIssues.Should().Contain(issue => issue.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectAsync_FallbackInfersBasicFieldTypesAndRootObject()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false)
            .DetectAsync(CreateRequest(payload: "{\"name\":\"Ada\",\"count\":2,\"active\":true,\"createdAt\":\"2026-05-14T10:30:00Z\"}"));

        response.Summary.Should().Contain("root object");
        response.ImportantFields.Should().Contain(field => field.FieldName == "name" && field.InferredType == "string");
        response.ImportantFields.Should().Contain(field => field.FieldName == "count" && field.InferredType == "integer");
        response.ImportantFields.Should().Contain(field => field.FieldName == "active" && field.InferredType == "boolean");
        response.ImportantFields.Should().Contain(field => field.FieldName == "createdAt" && field.InferredType == "datetime");
    }

    [Fact]
    public async Task DetectAsync_FallbackDetectsRootArray()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false)
            .DetectAsync(CreateRequest(payload: "[{\"sku\":\"SKU-1\",\"quantity\":2}]"));

        response.Summary.Should().Contain("root array");
        response.ImportantFields.Should().Contain(field => field.JsonPath == "$[0].sku");
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValues()
    {
        var builder = new PayloadSchemaDetectionPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 4000, MaskSensitiveValues = true }));
        var prompt = builder.BuildPrompt(CreateRequest(
            payload: "{\"accessToken\":\"super-secret\",\"password\":\"p@ss\"}",
            headers: new Dictionary<string, string> { ["Authorization"] = "Bearer abc", ["X-API-Key"] = "key-1" }));

        prompt.Should().Contain(PayloadSchemaDetectionPromptBuilder.MaskedValue);
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("Bearer abc");
        prompt.Should().NotContain("key-1");
    }

    [Fact]
    public void BuildPrompt_TruncatesLargePayload()
    {
        var builder = new PayloadSchemaDetectionPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 25 }));

        var prompt = builder.BuildPrompt(CreateRequest(payload: "{\"data\":\"" + new string('a', 200) + "\"}"));

        prompt.Should().Contain("truncated from");
    }

    [Fact]
    public async Task DetectAsync_ClampsConfidenceScore()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"eventId":"evt_1","detectedSchemaName":"Order","detectedEventType":"OrderCreated","summary":"ok","importantFields":[],"missingFields":[],"validationIssues":[],"suggestedDtoName":"OrderCreatedDto","confidenceScore":2.5,"riskLevel":"Low","generatedAtUtc":"2026-05-14T10:31:00Z"}
            """, 1));

        var response = await CreateAgent(llm.Object).DetectAsync(CreateRequest());

        response.ConfidenceScore.Should().Be(1);
    }

    [Fact]
    public async Task DetectAsync_GeneratedAtUtcIsUtc()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).DetectAsync(CreateRequest());

        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void GenerateSuggestedDtoName_UsesEventType()
    {
        PayloadSchemaDetectionAgent.GenerateSuggestedDtoName("order-created").Should().Be("OrderCreatedDto");
    }

    [Fact]
    public void AddPayloadSchemaDetectionServices_RegistersRequiredServices()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Enabled"] = "false" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<ILocalLlmClient>());
        services.AddAiOptions(config);
        services.AddAiPromptServices();
        services.AddPayloadSchemaDetectionServices();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IPayloadSchemaDetectionAgent>().Should().NotBeNull();
        provider.GetRequiredService<IPayloadSchemaDetectionPromptBuilder>().Should().NotBeNull();
    }

    private static PayloadSchemaDetectionAgent CreateAgent(ILocalLlmClient llmClient, bool enabled = true)
        => new(
            Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3", MaxPromptPayloadLength = 4000, MaskSensitiveValues = true }),
            new PayloadSchemaDetectionPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 4000, MaskSensitiveValues = true })),
            llmClient,
            NullLogger<PayloadSchemaDetectionAgent>.Instance);

    private static PayloadSchemaDetectionRequestDto CreateRequest(
        string payload = "{\"orderId\":\"ORD-1\",\"total\":12.5}",
        IDictionary<string, string>? headers = null)
        => new()
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            Source = "HookBridge.API",
            EventType = "OrderCreated",
            CustomerId = "cust_1",
            Payload = payload,
            Headers = headers,
            ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
        };
}
