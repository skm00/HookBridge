using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using HookBridge.AI.Worker.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookTransformationRecommendationAgentTests
{
    [Fact]
    public async Task RecommendAsync_ParsesValidAiResponseAndClampsConfidence()
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Success("""
            {"eventId":"evt_1","correlationId":"corr_1","summary":"Mapped.","recommendedMappings":[{"sourceJsonPath":"$.order_id","targetJsonPath":"$.orderId","sourceFieldName":"order_id","targetFieldName":"orderId","transformationType":"Rename","transformationExpression":"orderId = order_id","isRequired":true,"confidenceScore":1.5,"notes":"Variant."}],"missingTargetFields":[],"unmappedSourceFields":[],"transformationNotes":[],"generatedTransformationCode":"using System.Text.Json.Nodes; public static JsonObject Transform(JsonObject source) => new();","confidenceScore":1.4,"riskLevel":"Low","generatedAtUtc":"2026-05-14T10:31:00Z"}
            """, 5));

        var response = await CreateAgent(llm.Object).RecommendAsync(CreateRequest());

        response.RecommendedMappings.Should().ContainSingle(m => m.TargetFieldName == "orderId");
        response.ConfidenceScore.Should().Be(1);
        response.RecommendedMappings[0].ConfidenceScore.Should().Be(1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.Fallback!.UsedFallback.Should().BeFalse();
        response.GeneratedTransformationCode.Should().Contain("human review");
    }

    [Fact]
    public async Task RecommendAsync_FallsBackForInvalidAiJsonDisabledAiInvalidSourceAndMissingTarget()
    {
        var invalidJsonLlm = new Mock<ILocalLlmClient>();
        invalidJsonLlm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(LlmResponseResult.Success("not json", 1));

        var invalidAi = await CreateAgent(invalidJsonLlm.Object).RecommendAsync(CreateRequest());
        var disabled = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).RecommendAsync(CreateRequest());
        var invalidPayload = await CreateAgent(Mock.Of<ILocalLlmClient>()).RecommendAsync(CreateRequest(sourcePayload: "{bad json"));
        var missingTarget = await CreateAgent(Mock.Of<ILocalLlmClient>()).RecommendAsync(CreateRequest(targetSamplePayload: " ", targetSchema: " "));

        invalidAi.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        disabled.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        invalidPayload.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
        missingTarget.Fallback!.FallbackReason.Should().Be(AiFallbackReason.ConfigurationError);
    }

    [Fact]
    public async Task RecommendAsync_FallbackMapsExactCaseInsensitiveSnakeCaseAndCommonVariants()
    {
        var source = """{"order_id":"ORD-1","CustomerID":"cust-1","created_at":"2026-05-14T10:30:00Z","state":"new","extra":"value"}""";
        var target = """{"orderId":"string","customerId":"string","createdAt":"datetime","status":"string","missing":"string"}""";

        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).RecommendAsync(CreateRequest(sourcePayload: source, targetSamplePayload: target));

        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "order_id" && m.TargetFieldName == "orderId" && m.TransformationType == WebhookTransformationType.Rename);
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "CustomerID" && m.TargetFieldName == "customerId");
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "created_at" && m.TargetFieldName == "createdAt");
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "state" && m.TargetFieldName == "status");
        response.MissingTargetFields.Should().Contain("$.missing");
        response.UnmappedSourceFields.Should().Contain("$.extra");
        response.ConfidenceScore.Should().BeLessThan(0.6);
    }


    [Fact]
    public async Task RecommendAsync_FallbackMapsExactFieldNamesAsDirectMap()
    {
        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false)
            .RecommendAsync(CreateRequest(sourcePayload: "{\"status\":\"Created\"}", targetSamplePayload: "{\"status\":\"string\"}"));

        response.RecommendedMappings.Should().ContainSingle(m => m.SourceFieldName == "status" && m.TargetFieldName == "status" && m.TransformationType == WebhookTransformationType.DirectMap);
    }

    [Fact]
    public void BuildPrompt_MasksSensitiveValuesAndTruncatesPayloadAndSchema()
    {
        var builder = new WebhookTransformationPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 60, MaskSensitiveValues = true }));
        var prompt = builder.BuildPrompt(CreateRequest(
            sourcePayload: "{\"accessToken\":\"super-secret\",\"data\":\"" + new string('a', 200) + "\"}",
            targetSchema: "{\"properties\":{\"password\":{\"type\":\"string\"},\"data\":\"" + new string('b', 200) + "\"}}",
            headers: new Dictionary<string, string> { ["Authorization"] = "Bearer secret", ["X-API-Key"] = "abc" }));

        prompt.Should().Contain(WebhookTransformationPromptBuilder.MaskedValue);
        prompt.Should().NotContain("super-secret");
        prompt.Should().NotContain("Bearer secret");
        prompt.Should().Contain("truncated from");
    }


    [Fact]
    public async Task RecommendAsync_FallbackUsesTargetSchemaWhenSamplePayloadIsMissing()
    {
        var request = CreateRequest(sourcePayload: """{"identifier":"ORD-1","other":"value"}""", targetSamplePayload: " ", targetSchema: """{"properties":{"id":{"type":"string"}}}""");

        var response = await CreateAgent(Mock.Of<ILocalLlmClient>(), enabled: false).RecommendAsync(request);

        response.RecommendedMappings.Should().ContainSingle(m => m.SourceFieldName == "identifier" && m.TargetFieldName == "id");
        response.UnmappedSourceFields.Should().Contain("$.other");
    }

    [Fact]
    public async Task RecommendAsync_FallsBackWhenLlmReturnsFailureOrThrows()
    {
        var failureLlm = new Mock<ILocalLlmClient>();
        failureLlm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "provider down", 10));
        var throwingLlm = new Mock<ILocalLlmClient>();
        throwingLlm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var failureResponse = await CreateAgent(failureLlm.Object).RecommendAsync(CreateRequest());
        var thrownResponse = await CreateAgent(throwingLlm.Object).RecommendAsync(CreateRequest());

        failureResponse.Fallback!.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        failureResponse.Fallback.FallbackMessage.Should().Be("provider down");
        thrownResponse.Fallback!.FallbackReason.Should().Be(AiFallbackReason.UnknownError);
        thrownResponse.Fallback.FallbackMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task RecommendAsync_RejectsInvalidRequiredRequestMetadata()
    {
        var missingEventId = CreateRequest();
        missingEventId.EventId = " ";
        var missingPayload = CreateRequest();
        missingPayload.SourcePayload = null;
        var nonUtcReceivedAt = CreateRequest();
        nonUtcReceivedAt.ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Local);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateAgent(Mock.Of<ILocalLlmClient>()).RecommendAsync(missingEventId));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateAgent(Mock.Of<ILocalLlmClient>()).RecommendAsync(missingPayload));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateAgent(Mock.Of<ILocalLlmClient>()).RecommendAsync(nonUtcReceivedAt));
    }

    [Fact]
    public void BuildPrompt_WhenMaskingDisabled_KeepsSensitiveValuesAndHandlesNullHeaders()
    {
        var builder = new WebhookTransformationPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 4000, MaskSensitiveValues = false }));
        var request = CreateRequest(sourcePayload: """{"accessToken":"super-secret"}""");
        request.Headers = null!;

        var prompt = builder.BuildPrompt(request);

        prompt.Should().Contain("super-secret");
        prompt.Should().NotContain(WebhookTransformationPromptBuilder.MaskedValue);
    }

    [Fact]
    public void RequestValidator_CoversValidInvalidJsonAndUtcBranches()
    {
        var validator = new WebhookTransformationRecommendationRequestDtoValidator();
        var valid = validator.Validate(CreateRequest());
        var invalidJson = CreateRequest(sourcePayload: "not-json");
        var nonUtc = CreateRequest();
        nonUtc.ReceivedAtUtc = DateTime.SpecifyKind(nonUtc.ReceivedAtUtc, DateTimeKind.Unspecified);

        valid.IsValid.Should().BeTrue();
        validator.Validate(invalidJson).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.SourcePayload));
        validator.Validate(nonUtc).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.ReceivedAtUtc));
    }

    [Fact]
    public void MongoResult_FromResponse_CopiesMetadataAndUtcDates()
    {
        var response = new WebhookTransformationRecommendationResponseDto
        {
            EventId = "evt_1",
            CorrelationId = "corr_1",
            Summary = "summary",
            RecommendedMappings = new[] { new WebhookFieldMappingRecommendationDto { SourceFieldName = "id", TargetFieldName = "id" } },
            MissingTargetFields = new[] { "$.missing" },
            UnmappedSourceFields = new[] { "$.extra" },
            TransformationNotes = new[] { "review" },
            GeneratedTransformationCode = "// code",
            ConfidenceScore = 0.4,
            RiskLevel = "Medium",
            GeneratedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 30, 0), DateTimeKind.Utc),
            Model = "llama3",
            Provider = "Ollama",
            Fallback = new AiFallbackMetadataDto { UsedFallback = true }
        };

        var result = WebhookTransformationRecommendationResult.FromResponse(response, CreateRequest());

        result.RecommendedMappings.Should().ContainSingle();
        result.MissingTargetFields.Should().Contain("$.missing");
        result.UnmappedSourceFields.Should().Contain("$.extra");
        result.FallbackUsed.Should().BeTrue();
        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void RequiredServices_AreRegisteredInDi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration());
        services.AddAiPromptServices();
        services.AddSingleton<ILocalLlmClient>(_ => Mock.Of<ILocalLlmClient>());
        services.AddWebhookTransformationRecommendationServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IWebhookTransformationPromptBuilder>().Should().BeOfType<WebhookTransformationPromptBuilder>();
        provider.GetRequiredService<IWebhookTransformationRecommendationAgent>().Should().BeOfType<WebhookTransformationRecommendationAgent>();
    }

    [Fact]
    public void TopicConstant_IsExpectedValue()
        => AiKafkaTopics.TransformationRecommendation.Should().Be("hookbridge.ai.transformation-recommendation");

    private static WebhookTransformationRecommendationAgent CreateAgent(ILocalLlmClient llmClient, bool enabled = true)
    {
        var options = Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3-test", MaxPromptPayloadLength = 4000 });
        return new WebhookTransformationRecommendationAgent(options, new WebhookTransformationPromptBuilder(options), llmClient, NullLogger<WebhookTransformationRecommendationAgent>.Instance);
    }

    private static WebhookTransformationRecommendationRequestDto CreateRequest(object? sourcePayload = null, object? targetSamplePayload = null, object? targetSchema = null, IDictionary<string, string>? headers = null) => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        EventType = "OrderCreated",
        Source = "Shopify",
        CustomerId = "cust_1",
        SourcePayload = sourcePayload ?? "{\"order_id\":\"ORD-1\",\"status\":\"Created\"}",
        TargetSamplePayload = targetSamplePayload ?? "{\"orderId\":\"string\",\"status\":\"string\"}",
        TargetSchema = targetSchema,
        Headers = headers ?? new Dictionary<string, string>(),
        ReceivedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 30, 0), DateTimeKind.Utc)
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
