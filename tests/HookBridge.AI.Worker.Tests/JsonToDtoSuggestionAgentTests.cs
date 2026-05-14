using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.JsonToDtoSuggestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class JsonToDtoSuggestionAgentTests
{
    [Fact]
    public async Task SuggestAsync_ParsesValidAiResponse()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {
              "eventId":"evt_1",
              "correlationId":"corr_1",
              "suggestedRootClassName":"OrderCreatedDto",
              "namespace":"HookBridge.Contracts.Events",
              "generatedCode":"using System.Text.Json.Serialization;",
              "classes":[{"className":"OrderCreatedDto","properties":[{"propertyName":"OrderId","jsonName":"orderId","cSharpType":"string","isNullable":true,"isRequired":true,"description":"Order id."}],"description":"Order DTO."}],
              "summary":"Generated DTOs.",
              "validationNotes":[],
              "confidenceScore":1.4,
              "riskLevel":"Low",
              "generatedAtUtc":"2026-05-14T10:31:00Z"
            }
            """, 5));

        var response = await CreateAgent(llm.Object).SuggestAsync(CreateRequest());

        response.SuggestedRootClassName.Should().Be("OrderCreatedDto");
        response.Classes.Should().ContainSingle(dto => dto.ClassName == "OrderCreatedDto");
        response.ConfidenceScore.Should().Be(1);
        response.Fallback!.UsedFallback.Should().BeFalse();
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SuggestAsync_FallsBackWhenAiReturnsInvalidJson()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("not json", 5));

        var response = await CreateAgent(llm.Object).SuggestAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        response.GeneratedCode.Should().Contain("public sealed class OrderCreatedDto");
        response.GeneratedCode.Should().Contain("[JsonPropertyName(\"orderId\")]");
    }

    [Fact]
    public async Task SuggestAsync_FallsBackWhenAiIsDisabled()
    {
        var llm = new Mock<ILocalLlmClient>();

        var response = await CreateAgent(llm.Object, enabled: false).SuggestAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        response.ConfidenceScore.Should().BeLessThan(0.6);
        llm.Verify(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestAsync_HandlesInvalidPayloadJsonWithFallbackShell()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>()).SuggestAsync(CreateRequest(payload: "{bad json"));

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.RiskLevel.Should().Be("Medium");
        response.ValidationNotes.Should().Contain(note => note.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestAsync_FallbackGeneratesSimpleNestedArrayNullableDateTimeAndDecimalDtos()
    {
        var payload = """
        {"orderId":"ORD-1","totalAmount":129.50,"createdAt":"2026-05-14T10:30:00Z","optional":null,"customer":{"id":"C001"},"items":[{"sku":"SKU-1","quantity":2}]}
        """;

        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).SuggestAsync(CreateRequest(payload: payload));

        response.GeneratedCode.Should().Contain("public sealed class OrderCreatedDto");
        response.GeneratedCode.Should().Contain("public decimal TotalAmount { get; set; }");
        response.GeneratedCode.Should().Contain("public DateTime CreatedAt { get; set; }");
        response.GeneratedCode.Should().Contain("public object? Optional { get; set; }");
        response.GeneratedCode.Should().Contain("public CustomerDto? Customer { get; set; }");
        response.GeneratedCode.Should().Contain("public List<OrderCreatedItemDto>? Items { get; set; }");
        response.Classes.Should().Contain(dto => dto.ClassName == "CustomerDto");
        response.Classes.Should().Contain(dto => dto.ClassName == "OrderCreatedItemDto");
    }

    [Fact]
    public async Task SuggestAsync_GeneratesRootClassNameFromEventType()
    {
        var request = CreateRequest(rootClassName: null, eventType: "order.created");

        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).SuggestAsync(request);

        response.SuggestedRootClassName.Should().Be("OrderCreatedDto");
    }

    [Theory]
    [InlineData("1Bad")]
    [InlineData("class")]
    public async Task SuggestAsync_RejectsInvalidClassName(string className)
    {
        var act = () => CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).SuggestAsync(CreateRequest(rootClassName: className));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*RootClassName*");
    }

    [Theory]
    [InlineData("HookBridge..Contracts")]
    [InlineData("HookBridge.1Bad")]
    public async Task SuggestAsync_RejectsInvalidNamespace(string dtoNamespace)
    {
        var act = () => CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).SuggestAsync(CreateRequest(dtoNamespace: dtoNamespace));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Namespace*");
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValues()
    {
        var builder = new JsonToDtoPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 4000, MaskSensitiveValues = true }));

        var prompt = builder.BuildPrompt(CreateRequest(payload: "{\"accessToken\":\"super-secret\",\"password\":\"p@ss\",\"Authorization\":\"Bearer abc\"}"));

        prompt.Should().Contain(JsonToDtoPromptBuilder.MaskedValue);
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("Bearer abc");
    }

    [Fact]
    public void BuildPrompt_TruncatesLargePayload()
    {
        var builder = new JsonToDtoPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 25 }));

        var prompt = builder.BuildPrompt(CreateRequest(payload: "{\"data\":\"" + new string('a', 200) + "\"}"));

        prompt.Should().Contain("[truncated from");
    }

    [Fact]
    public void AddAiServiceExtensions_RegisterJsonToDtoSuggestionServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration(enabled: true));
        services.AddAiPromptServices();
        services.AddSingleton<ILocalLlmClient>(_ => Mock.Of<ILocalLlmClient>());
        services.AddJsonToDtoSuggestionServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IJsonToDtoPromptBuilder>().Should().BeOfType<JsonToDtoPromptBuilder>();
        provider.GetRequiredService<IJsonToDtoSuggestionAgent>().Should().BeOfType<JsonToDtoSuggestionAgent>();
        AiKafkaTopics.DtoSuggestion.Should().Be("hookbridge.ai.dto-suggestion");
    }

    private static JsonToDtoSuggestionAgent CreateAgent(ILocalLlmClient llmClient, bool enabled = true)
        => new(
            Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3", MaxPromptPayloadLength = 4000 }),
            new JsonToDtoPromptBuilder(Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3", MaxPromptPayloadLength = 4000 })),
            llmClient,
            NullLogger<JsonToDtoSuggestionAgent>.Instance);

    private static JsonToDtoSuggestionRequestDto CreateRequest(
        string payload = "{\"orderId\":\"ORD-1\",\"total\":12.5}",
        string? rootClassName = "OrderCreatedDto",
        string? eventType = "OrderCreated",
        string? dtoNamespace = "HookBridge.Contracts.Events")
        => new()
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            Source = "HookBridge.API",
            EventType = eventType,
            CustomerId = "cust_1",
            RootClassName = rootClassName,
            Namespace = dtoNamespace,
            Payload = payload,
            ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
        };

    private static IConfiguration BuildConfiguration(bool enabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AiOptions.SectionName}:Enabled"] = enabled.ToString(),
                [$"{AiOptions.SectionName}:Provider"] = enabled ? "Ollama" : string.Empty,
                [$"{AiOptions.SectionName}:Model"] = enabled ? "llama3-test" : string.Empty,
                [$"{AiOptions.SectionName}:Endpoint"] = enabled ? "http://localhost:11434" : string.Empty
            })
            .Build();
}
