using System.Text.Json;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class FluentValidationRuleGenerationAgentTests
{
    [Fact]
    public async Task GenerateAsync_ParsesValidAiResponseAndClampsConfidence()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {
              "eventId":"evt_1",
              "correlationId":"corr_1",
              "validatorClassName":"OrderCreatedDtoValidator",
              "namespace":"HookBridge.Contracts.Events",
              "generatedValidatorCode":"using FluentValidation;",
              "rules":[{"propertyName":"OrderId","ruleType":"NotEmpty","ruleExpression":".NotEmpty()","errorMessage":"OrderId is required.","severity":"Error","description":"Required."}],
              "summary":"Generated validator.",
              "validationNotes":[],
              "confidenceScore":1.4,
              "riskLevel":"Low",
              "generatedAtUtc":"2026-05-14T10:31:00Z"
            }
            """, 5));

        var response = await CreateAgent(llm.Object).GenerateAsync(CreateRequest());

        response.ValidatorClassName.Should().Be("OrderCreatedDtoValidator");
        response.Rules.Should().ContainSingle(rule => rule.RuleType == "NotEmpty");
        response.ConfidenceScore.Should().Be(1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.Fallback!.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_FallsBackForInvalidAiJson()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("not json", 5));

        var response = await CreateAgent(llm.Object).GenerateAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        response.GeneratedValidatorCode.Should().Contain("AbstractValidator<OrderCreatedDto>");
    }

    [Fact]
    public async Task GenerateAsync_FallsBackWhenAiDisabled()
    {
        var llm = new Mock<ILocalLlmClient>();

        var response = await CreateAgent(llm.Object, enabled: false).GenerateAsync(CreateRequest());

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        response.ConfidenceScore.Should().BeLessThan(0.6);
        llm.Verify(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_FallsBackWhenGeneratedDtoCodeMissing()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>()).GenerateAsync(CreateRequest(generatedDtoCode: ""));

        response.Fallback!.UsedFallback.Should().BeTrue();
        response.ValidationNotes.Should().Contain(note => note.Contains("Generated DTO code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateAsync_HandlesInvalidPayloadJsonWithFallbackShell()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>()).GenerateAsync(CreateRequest(payload: "{bad json"));

        response.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        response.GeneratedValidatorCode.Should().Contain("OrderCreatedDtoValidator");
        response.ValidationNotes.Should().Contain(note => note.Contains("invalid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateAsync_FallbackGeneratesExpectedRuleTypes()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest());

        response.GeneratedValidatorCode.Should().Contain(".NotEmpty()");
        response.GeneratedValidatorCode.Should().Contain(".EmailAddress()");
        response.GeneratedValidatorCode.Should().Contain("Uri.TryCreate(value, UriKind.Absolute, out _)");
        response.GeneratedValidatorCode.Should().Contain("TotalAmount")
            .And.Contain(".GreaterThanOrEqualTo(0)");
        response.GeneratedValidatorCode.Should().Contain("Quantity")
            .And.Contain(".GreaterThanOrEqualTo(0)");
        response.GeneratedValidatorCode.Should().Contain("CreatedAt")
            .And.Contain("DateTimeKind.Utc");
        response.GeneratedValidatorCode.Should().Contain("Items")
            .And.Contain(".NotNull()");
        response.Rules.Should().Contain(rule => rule.PropertyName == "OrderId" && rule.RuleType == "NotEmpty");
        response.Rules.Should().Contain(rule => rule.PropertyName == "CustomerEmail" && rule.RuleType == "EmailAddress");
        response.Rules.Should().Contain(rule => rule.PropertyName == "CallbackUrl" && rule.RuleType == "Url");
        response.Rules.Should().Contain(rule => rule.PropertyName == "TotalAmount" && rule.RuleType == "GreaterThanOrEqualTo");
        response.Rules.Should().Contain(rule => rule.PropertyName.EndsWith("Quantity", StringComparison.Ordinal) && rule.RuleType == "GreaterThanOrEqualTo");
        response.Rules.Should().Contain(rule => rule.PropertyName == "CreatedAt" && rule.RuleType == "UtcDateTime");
        response.Rules.Should().Contain(rule => rule.PropertyName == "Items" && rule.RuleType == "NotNull");
    }

    [Fact]
    public async Task GenerateAsync_GeneratesValidatorClassName()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest());

        response.ValidatorClassName.Should().Be("OrderCreatedDtoValidator");
        response.GeneratedValidatorCode.Should().Contain("public sealed class OrderCreatedDtoValidator");
    }

    [Theory]
    [InlineData("1Bad")]
    [InlineData("class")]
    public async Task GenerateAsync_RejectsInvalidClassName(string className)
    {
        Func<Task> act = async () => await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest(rootClassName: className));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*RootClassName*");
    }

    [Theory]
    [InlineData("HookBridge..Contracts")]
    [InlineData("HookBridge.1Bad")]
    public async Task GenerateAsync_RejectsInvalidNamespace(string dtoNamespace)
    {
        Func<Task> act = async () => await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest(dtoNamespace: dtoNamespace));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Namespace*");
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValuesAndTruncatesLargeInputs()
    {
        var builder = new FluentValidationPromptBuilder();
        var payload = "{\"authorization\":\"Bearer secret-token\",\"orderId\":\"ORD-1\",\"blob\":\"" + new string('x', 9_000) + "\"}";
        var dtoCode = "public sealed class OrderCreatedDto { public string Password { get; set; } = \"secret\"; }" + new string('y', 13_000);

        var prompt = builder.BuildPrompt(CreateRequest(payload: payload, generatedDtoCode: dtoCode));

        prompt.Should().Contain("***MASKED***");
        prompt.Should().NotContain("Bearer secret-token");
        prompt.Should().NotContain("= \"secret\"");
        prompt.Should().Contain("truncated from");
    }

    [Fact]
    public void AddAiServiceExtensions_RegisterFluentValidationRuleGenerationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<AiOptions>(options =>
        {
            options.Enabled = false;
            options.Provider = "Test";
            options.Model = "test-model";
        });
        services.AddSingleton<ILocalLlmClient>(_ => Mock.Of<ILocalLlmClient>());
        services.AddAiPromptServices();
        services.AddFluentValidationRuleGenerationServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IFluentValidationPromptBuilder>().Should().BeOfType<FluentValidationPromptBuilder>();
        provider.GetRequiredService<IFluentValidationRuleGenerationAgent>().Should().BeOfType<FluentValidationRuleGenerationAgent>();
    }

    private static FluentValidationRuleGenerationAgent CreateAgent(ILocalLlmClient llmClient, bool enabled = true)
        => new(
            Options.Create(new AiOptions { Enabled = enabled, Provider = "Test", Model = "test-model", Endpoint = "http://localhost" }),
            new FluentValidationPromptBuilder(),
            llmClient,
            NullLogger<FluentValidationRuleGenerationAgent>.Instance);

    private static FluentValidationRuleGenerationRequestDto CreateRequest(
        string? payload = null,
        string? generatedDtoCode = null,
        string rootClassName = "OrderCreatedDto",
        string? dtoNamespace = "HookBridge.Contracts.Events")
    {
        payload ??= """
        {
          "orderId":"ORD-1",
          "status":"Created",
          "totalAmount":129.50,
          "customerEmail":"customer@example.com",
          "callbackUrl":"https://customer.example.com/webhook",
          "createdAt":"2026-05-14T10:30:00Z",
          "items":[{"sku":"SKU-1","quantity":2}]
        }
        """;

        generatedDtoCode ??= "public sealed class OrderCreatedDto { public string? OrderId { get; set; } }";

        return new FluentValidationRuleGenerationRequestDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            EventType = "OrderCreated",
            Source = "tests",
            CustomerId = "customer_1",
            RootClassName = rootClassName,
            Namespace = dtoNamespace,
            Payload = payload,
            GeneratedDtoCode = generatedDtoCode,
            RequiredFields = ["orderId", "status", "items"],
            DetectedFields = [new PayloadFieldInsightDto { FieldName = "orderId", JsonPath = "$.orderId", InferredType = "string", IsRequired = true, SampleValue = "ORD-1" }],
            ReceivedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 30, 0), DateTimeKind.Utc)
        };
    }
}
