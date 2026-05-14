using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;
using Microsoft.Extensions.Configuration;
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
            {"eventId":"evt_1","correlationId":"corr_1","validatorClassName":"OrderCreatedDtoValidator","namespace":"HookBridge.Contracts.Events","generatedValidatorCode":"using FluentValidation; public sealed class OrderCreatedDtoValidator : AbstractValidator<OrderCreatedDto> {}","rules":[{"propertyName":"OrderId","ruleType":"NotEmpty","ruleExpression":".NotEmpty()","errorMessage":"OrderId is required.","severity":"Error","description":"Required."}],"summary":"Generated.","validationNotes":[],"confidenceScore":1.4,"riskLevel":"Low","generatedAtUtc":"2026-05-14T10:31:00Z"}
            """, 5));

        var response = await CreateAgent(llm.Object).GenerateAsync(CreateRequest());

        response.ValidatorClassName.Should().Be("OrderCreatedDtoValidator");
        response.Rules.Should().ContainSingle(rule => rule.PropertyName == "OrderId");
        response.ConfidenceScore.Should().Be(1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.Fallback!.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_FallsBackForInvalidAiJsonDisabledAiMissingDtoAndInvalidPayload()
    {
        var invalidJsonLlm = new Mock<ILocalLlmClient>();
        invalidJsonLlm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success("not json", 1));

        var invalidAi = await CreateAgent(invalidJsonLlm.Object).GenerateAsync(CreateRequest());
        var disabled = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest());
        var missingDto = await CreateAgent(Mock.Of<ILocalLlmClient>()).GenerateAsync(CreateRequest(generatedDtoCode: " "));
        var invalidPayload = await CreateAgent(Mock.Of<ILocalLlmClient>()).GenerateAsync(CreateRequest(payload: "{bad json"));

        invalidAi.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        disabled.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        missingDto.Fallback!.UsedFallback.Should().BeTrue();
        invalidPayload.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        invalidPayload.GeneratedValidatorCode.Should().Contain("OrderCreatedDtoValidator");
    }

    [Fact]
    public async Task GenerateAsync_FallbackInfersExpectedRules()
    {
        var payload = """
        {"orderId":"ORD-1","customerEmail":"customer@example.com","callbackUrl":"https://example.com","totalAmount":10.5,"quantity":2,"itemCount":1,"createdAt":"2026-05-14T10:30:00Z","items":[{"sku":"SKU-1"}]}
        """;

        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).GenerateAsync(CreateRequest(payload: payload, requiredFields: ["items", "status"]));

        response.GeneratedValidatorCode.Should().Contain("RuleFor(x => x.OrderId)");
        response.GeneratedValidatorCode.Should().Contain(".NotEmpty()");
        response.GeneratedValidatorCode.Should().Contain(".EmailAddress()");
        response.GeneratedValidatorCode.Should().Contain("Uri.TryCreate");
        response.GeneratedValidatorCode.Should().Contain("RuleFor(x => x.TotalAmount)");
        response.GeneratedValidatorCode.Should().Contain("RuleFor(x => x.Quantity)");
        response.GeneratedValidatorCode.Should().Contain("RuleFor(x => x.ItemCount)");
        response.GeneratedValidatorCode.Should().Contain("DateTimeKind.Utc");
        response.GeneratedValidatorCode.Should().Contain("RuleFor(x => x.Items)");
        response.GeneratedValidatorCode.Should().Contain("public sealed class OrderCreatedDtoValidator : AbstractValidator<OrderCreatedDto>");
        response.ConfidenceScore.Should().BeLessThan(0.6);
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
    public void BuildPrompt_MasksSensitiveValuesAndTruncatesPayloadAndDtoCode()
    {
        var builder = new FluentValidationPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 40, MaskSensitiveValues = true }));
        var request = CreateRequest(payload: "{\"accessToken\":\"super-secret\",\"data\":\"" + new string('a', 200) + "\"}", generatedDtoCode: "public string Password { get; set; } = \"p@ss\";" + new string('b', 200));

        var prompt = builder.BuildPrompt(request);

        prompt.Should().Contain(FluentValidationPromptBuilder.MaskedValue);
        prompt.Should().Contain("[truncated from");
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("p@ss");
    }

    [Fact]
    public async Task GenerateAsync_NormalizesAiDefaultsAndRemovesSensitiveGeneratedCodeLines()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"generatedValidatorCode":"using FluentValidation;\n// AccessToken secret\npublic sealed class OrderCreatedDtoValidator : AbstractValidator<OrderCreatedDto> {}","confidenceScore":-0.5,"riskLevel":"Unexpected","generatedAtUtc":"2026-05-14T10:31:00"}
            """, 5));

        var response = await CreateAgent(llm.Object).GenerateAsync(CreateRequest());

        response.EventId.Should().Be("evt_1");
        response.ValidatorClassName.Should().Be("OrderCreatedDtoValidator");
        response.ConfidenceScore.Should().Be(0);
        response.RiskLevel.Should().Be("Unknown");
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.GeneratedValidatorCode.Should().NotContain("AccessToken");
    }

    [Fact]
    public void AddAiServiceExtensions_RegisterFluentValidationRuleGenerationServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration());
        services.AddAiPromptServices();
        services.AddSingleton<ILocalLlmClient>(_ => Mock.Of<ILocalLlmClient>());
        services.AddFluentValidationRuleGenerationServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IFluentValidationPromptBuilder>().Should().BeOfType<FluentValidationPromptBuilder>();
        provider.GetRequiredService<IFluentValidationRuleGenerationAgent>().Should().BeOfType<FluentValidationRuleGenerationAgent>();
        AiKafkaTopics.ValidationRuleGeneration.Should().Be("hookbridge.ai.validation-rule-generation");
    }

    private static FluentValidationRuleGenerationAgent CreateAgent(ILocalLlmClient llmClient, bool enabled = true)
        => new(
            Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3", MaxPromptPayloadLength = 4000, MaskSensitiveValues = true }),
            new FluentValidationPromptBuilder(Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3", MaxPromptPayloadLength = 4000, MaskSensitiveValues = true })),
            llmClient,
            NullLogger<FluentValidationRuleGenerationAgent>.Instance);

    private static FluentValidationRuleGenerationRequestDto CreateRequest(
        string payload = "{\"orderId\":\"ORD-1\",\"totalAmount\":12.5}",
        string rootClassName = "OrderCreatedDto",
        string? dtoNamespace = "HookBridge.Contracts.Events",
        string? generatedDtoCode = "public sealed class OrderCreatedDto { public string? OrderId { get; set; } public decimal TotalAmount { get; set; } }",
        IReadOnlyList<string>? requiredFields = null)
        => new()
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            Source = "HookBridge.API",
            EventType = "OrderCreated",
            CustomerId = "cust_1",
            RootClassName = rootClassName,
            Namespace = dtoNamespace,
            Payload = payload,
            GeneratedDtoCode = generatedDtoCode,
            RequiredFields = requiredFields ?? ["orderId"],
            ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
        };

    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AiOptions.SectionName}:Enabled"] = "true",
            [$"{AiOptions.SectionName}:Provider"] = "Ollama",
            [$"{AiOptions.SectionName}:Model"] = "llama3-test",
            [$"{AiOptions.SectionName}:Endpoint"] = "http://localhost:11434"
        }).Build();
}
