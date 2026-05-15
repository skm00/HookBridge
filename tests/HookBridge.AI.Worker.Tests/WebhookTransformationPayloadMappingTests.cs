using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
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
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class WebhookTransformationPayloadMappingTests
{
    [Fact]
    public async Task FallbackMapping_MapsExactFieldsWithHighConfidence()
    {
        var response = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"orderId":"ORD-1001","status":"Created","customerId":"CUST-42"}""",
            targetSamplePayload: """{"orderId":"string","status":"string","customerId":"string"}"""));

        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "orderId" && m.TargetFieldName == "orderId" && m.TransformationType == WebhookTransformationType.DirectMap);
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "status" && m.TargetFieldName == "status" && m.TransformationType == WebhookTransformationType.DirectMap);
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "customerId" && m.TargetFieldName == "customerId" && m.TransformationType == WebhookTransformationType.DirectMap);
        response.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.8);
        response.RecommendedMappings.Should().OnlyContain(mapping => mapping.ConfidenceScore >= 0.8);
    }

    [Fact]
    public async Task FallbackMapping_MapsCaseInsensitiveFieldsWithHighConfidence()
    {
        var response = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"OrderId":"ORD-1001","STATUS":"Created","CustomerID":"CUST-42"}""",
            targetSamplePayload: """{"orderId":"string","status":"string","customerId":"string"}"""));

        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "OrderId" && m.TargetFieldName == "orderId" && m.TransformationType == WebhookTransformationType.Rename && m.ConfidenceScore >= 0.8);
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "STATUS" && m.TargetFieldName == "status" && m.ConfidenceScore >= 0.8);
        response.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "CustomerID" && m.TargetFieldName == "customerId" && m.ConfidenceScore >= 0.8);
    }

    [Fact]
    public async Task FallbackMapping_MapsSnakeCaseAndCommonAliasesWithMediumConfidence()
    {
        var snakeCase = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"order_id":"ORD-1001","customer_id":"CUST-42","created_at":"2026-05-14T10:30:00Z"}""",
            targetSamplePayload: """{"orderId":"string","customerId":"string","createdAt":"datetime"}"""));
        var aliases = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"id":"abc","state":"Created","amount":10,"createdAt":"2026-05-14T10:30:00Z"}""",
            targetSamplePayload: """{"identifier":"string","status":"string","totalAmount":"decimal","createdAtUtc":"datetime"}"""));

        snakeCase.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "order_id" && m.TargetFieldName == "orderId" && m.TransformationType == WebhookTransformationType.Rename);
        snakeCase.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "customer_id" && m.TargetFieldName == "customerId");
        snakeCase.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "created_at" && m.TargetFieldName == "createdAt");
        aliases.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "id" && m.TargetFieldName == "identifier");
        aliases.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "state" && m.TargetFieldName == "status");
        aliases.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "amount" && m.TargetFieldName == "totalAmount");
        aliases.RecommendedMappings.Should().Contain(m => m.SourceFieldName == "createdAt" && m.TargetFieldName == "createdAtUtc");
        snakeCase.RecommendedMappings.Concat(aliases.RecommendedMappings).Where(m => m.TransformationType == WebhookTransformationType.Rename).Should().OnlyContain(m => m.ConfidenceScore >= 0.6 && m.ConfidenceScore < 0.8);
    }

    [Fact]
    public async Task FallbackMapping_MapsNestedObjectsAndArraysUsingJsonPaths()
    {
        var nested = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: ReadPayload("nested-object-source.json"),
            targetSamplePayload: ReadPayload("nested-object-target.json")));
        var array = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: ReadPayload("array-source.json"),
            targetSamplePayload: ReadPayload("array-target.json")));

        nested.RecommendedMappings.Should().Contain(m => m.SourceJsonPath == "$.customer.id" && m.TargetJsonPath == "$.customerId" && m.SourceFieldName == "id" && m.TargetFieldName == "customerId");
        nested.RecommendedMappings.Should().Contain(m => m.SourceJsonPath == "$.customer.name" && m.TargetJsonPath == "$.customerName");
        nested.RecommendedMappings.Should().Contain(m => m.SourceJsonPath == "$.address.city" && m.TargetJsonPath == "$.city");
        array.RecommendedMappings.Should().Contain(m => m.SourceJsonPath == "$.items[].sku" && m.TargetJsonPath == "$.lineItems[].sku");
        array.RecommendedMappings.Should().Contain(m => m.SourceJsonPath == "$.items[].quantity" && m.TargetJsonPath == "$.lineItems[].quantity");
    }

    [Fact]
    public async Task FallbackMapping_ReportsMissingTargetsAndUnmappedSourcesWithRiskRules()
    {
        var missingTarget = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"orderId":"ORD-1001"}""",
            targetSamplePayload: """{"orderId":"string","deliveryDate":"datetime"}"""));
        var unimportantExtra = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"orderId":"ORD-1001","debug":"trace"}""",
            targetSamplePayload: """{"orderId":"string"}"""));
        var importantExtra = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"orderId":"ORD-1001","amount":10}""",
            targetSamplePayload: """{"orderId":"string"}"""));

        missingTarget.MissingTargetFields.Should().Contain("$.deliveryDate");
        missingTarget.TransformationNotes.Should().Contain(note => note.Contains("manual mapping required", StringComparison.OrdinalIgnoreCase));
        unimportantExtra.UnmappedSourceFields.Should().Contain("$.debug");
        unimportantExtra.RiskLevel.Should().Be("Low");
        importantExtra.UnmappedSourceFields.Should().Contain("$.amount");
        importantExtra.RiskLevel.Should().Be("Medium");
    }

    [Fact]
    public async Task AiParsing_ParsesAllTransformationTypesAndClampsScores()
    {
        var mappings = Enum.GetValues<WebhookTransformationType>()
            .Where(type => type is WebhookTransformationType.DirectMap or WebhookTransformationType.Rename or WebhookTransformationType.TypeConversion or WebhookTransformationType.DateFormat or WebhookTransformationType.DefaultValue or WebhookTransformationType.ConstantValue or WebhookTransformationType.Ignore or WebhookTransformationType.Custom)
            .Select((type, index) => $"{{\"sourceJsonPath\":\"$.s{index}\",\"targetJsonPath\":\"$.t{index}\",\"sourceFieldName\":\"s{index}\",\"targetFieldName\":\"t{index}\",\"transformationType\":\"{type}\",\"transformationExpression\":\"t = s\",\"isRequired\":true,\"confidenceScore\":{(index == 0 ? -1 : 1.5)}}}")
            .ToArray();
        var json = $"{{\"summary\":\"ok\",\"recommendedMappings\":[{string.Join(",", mappings)}],\"missingTargetFields\":[],\"unmappedSourceFields\":[],\"transformationNotes\":[],\"generatedTransformationCode\":\"// code\",\"confidenceScore\":1.5,\"riskLevel\":\"Low\",\"generatedAtUtc\":\"2026-05-14T10:31:00Z\"}}";

        var response = await CreateAgent(aiResponse: json).RecommendAsync(CreateRequest());

        response.RecommendedMappings.Select(m => m.TransformationType).Should().Contain(new[]
        {
            WebhookTransformationType.DirectMap,
            WebhookTransformationType.Rename,
            WebhookTransformationType.TypeConversion,
            WebhookTransformationType.DateFormat,
            WebhookTransformationType.DefaultValue,
            WebhookTransformationType.ConstantValue,
            WebhookTransformationType.Ignore,
            WebhookTransformationType.Custom
        });
        response.ConfidenceScore.Should().Be(1);
        response.RecommendedMappings.Should().OnlyContain(m => m.ConfidenceScore is >= 0 and <= 1);
    }

    [Theory]
    [InlineData("not json", AiFallbackReason.InvalidJson)]
    [InlineData("", AiFallbackReason.InvalidJson)]
    [InlineData("{}", AiFallbackReason.InvalidJson)]
    [InlineData("{\"summary\":\"ok\",\"recommendedMappings\":[{\"transformationType\":\"Bogus\"}],\"confidenceScore\":0.5,\"riskLevel\":\"Low\",\"generatedAtUtc\":\"2026-05-14T10:31:00Z\"}", AiFallbackReason.InvalidJson)]
    public async Task AiParsing_InvalidResponsesUseFallback(string aiResponse, AiFallbackReason reason)
    {
        var response = await CreateAgent(aiResponse: aiResponse).RecommendAsync(CreateRequest());

        response.Fallback.Should().NotBeNull();
        response.Fallback!.UsedFallback.Should().BeTrue();
        response.Fallback.FallbackReason.Should().Be(reason);
    }

    [Fact]
    public async Task AiDisabledAndLlmFailures_UseFallbackWithSafeMetadata()
    {
        var disabled = await CreateAgent(enabled: false).RecommendAsync(CreateRequest());
        var providerUnavailable = await CreateAgent(llmResult: LlmResponseResult.Failure(AiFallbackReason.ProviderUnavailable, "offline", 1)).RecommendAsync(CreateRequest());
        var timeout = await CreateAgent(llmResult: LlmResponseResult.Failure(AiFallbackReason.Timeout, "timeout", 1)).RecommendAsync(CreateRequest());
        var invalidJson = await CreateAgent(llmResult: LlmResponseResult.Failure(AiFallbackReason.InvalidJson, "invalid", 1)).RecommendAsync(CreateRequest());

        disabled.Fallback!.FallbackReason.Should().Be(AiFallbackReason.AiDisabled);
        disabled.Provider.Should().Be("Ollama");
        disabled.Model.Should().Be("llama3-test");
        providerUnavailable.Fallback!.FallbackReason.Should().Be(AiFallbackReason.ProviderUnavailable);
        timeout.Fallback!.FallbackReason.Should().Be(AiFallbackReason.Timeout);
        invalidJson.Fallback!.FallbackReason.Should().Be(AiFallbackReason.InvalidJson);
    }

    [Fact]
    public void PromptBuilder_IncludesRequiredInstructionsAllowedTypesMaskingAndTruncation()
    {
        var builder = new WebhookTransformationPromptBuilder(Options.Create(new AiOptions { MaxPromptPayloadLength = 80, MaskSensitiveValues = true }));
        var request = CreateRequest(
            sourcePayload: "{\"access_token\":\"token-value\",\"client_secret\":\"secret-value\",\"password\":\"password-value\",\"data\":\"" + new string('a', 250) + "\"}",
            targetSchema: "{\"properties\":{\"id\":{\"type\":\"string\"}},\"padding\":\"" + new string('b', 250) + "\"}",
            targetSamplePayload: "{\"id\":\"string\"}",
            existingMappingRules: new { rules = new string('c', 250) },
            headers: new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer secret",
                ["Cookie"] = "session=secret",
                ["X-API-Key"] = "api-secret"
            });

        var prompt = builder.BuildPrompt(request);

        prompt.Should().Contain("sourcePayload").And.Contain("targetSchema").And.Contain("targetSamplePayload");
        prompt.Should().Contain("Return strict JSON only");
        prompt.Should().Contain("Do not invent unavailable source fields");
        prompt.Should().Contain("human review");
        foreach (var type in Enum.GetNames<WebhookTransformationType>()) prompt.Should().Contain(type);
        prompt.Should().Contain(WebhookTransformationPromptBuilder.MaskedValue);
        prompt.Should().NotContain("Bearer secret").And.NotContain("session=secret").And.NotContain("api-secret");
        prompt.Should().NotContain("token-value").And.NotContain("secret-value").And.NotContain("password-value");
        prompt.Should().Contain("truncated from");
        prompt.TrimStart().Should().StartWith("You are HookBridge AI");
    }

    [Fact]
    public async Task GeneratedTransformationCode_DoesNotIncludeSecretValues()
    {
        var response = await CreateAgent(enabled: false).RecommendAsync(CreateRequest(
            sourcePayload: """{"password":"secret-password","orderId":"ORD-1001"}""",
            targetSamplePayload: """{"orderId":"string"}"""));

        response.GeneratedTransformationCode.Should().NotContain("secret-password");
        response.GeneratedTransformationCode.Should().Contain("human review");
    }

    [Fact]
    public void Validators_CoverRequiredJsonUtcUrlAndConfidenceRules()
    {
        var validator = new WebhookTransformationRecommendationRequestDtoValidator();
        var missingEventId = CreateRequest();
        missingEventId.EventId = string.Empty;
        var missingPayload = CreateRequest();
        missingPayload.SourcePayload = null;
        var invalidJson = CreateRequest(sourcePayload: ReadPayload("invalid-json-source.json"));
        var nonUtc = CreateRequest();
        nonUtc.ReceivedAtUtc = DateTime.SpecifyKind(nonUtc.ReceivedAtUtc, DateTimeKind.Local);
        var badUrl = CreateRequest(targetUrl: "not-a-url");

        validator.Validate(missingEventId).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.EventId));
        validator.Validate(missingPayload).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.SourcePayload));
        validator.Validate(invalidJson).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.SourcePayload));
        validator.Validate(nonUtc).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.ReceivedAtUtc));
        validator.Validate(badUrl).Errors.Should().Contain(error => error.PropertyName == nameof(WebhookTransformationRecommendationRequestDto.TargetUrl));
        new WebhookTransformationRecommendationResponseDto { GeneratedAtUtc = DateTime.UtcNow, ConfidenceScore = 1 }.ConfidenceScore.Should().BeInRange(0, 1);
    }

    [Fact]
    public void Mapper_MapsRequestResponseResultAndOptionalMetadataSafely()
    {
        var request = CreateRequest();
        var fromRequest = WebhookTransformationRecommendationResult.FromRequest(request);
        var response = new WebhookTransformationRecommendationResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            Summary = "summary",
            RecommendedMappings = new[] { new WebhookFieldMappingRecommendationDto { SourceFieldName = "id", TargetFieldName = "identifier", TransformationType = WebhookTransformationType.Rename } },
            MissingTargetFields = Array.Empty<string>(),
            UnmappedSourceFields = Array.Empty<string>(),
            TransformationNotes = Array.Empty<string>(),
            GeneratedTransformationCode = "// code",
            ConfidenceScore = 0.75,
            RiskLevel = "Low",
            GeneratedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 31, 0), DateTimeKind.Utc),
            Provider = "Ollama",
            Model = "llama3-test",
            PromptName = "WebhookTransformationRecommendation",
            PromptVersion = "1.0.0",
            PromptHash = "hash",
            Fallback = new AiFallbackMetadataDto { UsedFallback = true }
        };

        var result = WebhookTransformationRecommendationResult.FromResponse(response, request);
        result.ApprovalId = "approval-1";
        var roundTrip = result.ToResponseDto();

        fromRequest.EventId.Should().Be(request.EventId);
        result.EventType.Should().Be(request.EventType);
        result.PromptName.Should().Be("WebhookTransformationRecommendation");
        result.PromptVersion.Should().Be("1.0.0");
        result.PromptHash.Should().Be("hash");
        result.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.PendingReview);
        result.ApprovalId.Should().Be("approval-1");
        roundTrip.RecommendedMappings.Should().ContainSingle();
        roundTrip.Fallback!.UsedFallback.Should().BeTrue();
    }

    [Fact]
    public async Task Repository_UsesMongoCollectionForInsertQueriesSearchAndIndexes()
    {
        var collection = CreateCollectionReturning(new[] { new WebhookTransformationRecommendationResult { EventId = "evt_1", CorrelationId = "corr_1", CustomerId = "cust_1", EventType = "OrderCreated", RiskLevel = "Low" } });
        var repository = CreateRepository(collection.Object);
        var result = new WebhookTransformationRecommendationResult { EventId = "evt_1", CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };

        await repository.InsertAsync(result);
        (await repository.GetByEventIdAsync("evt_1")).Should().NotBeNull();
        (await repository.GetByCorrelationIdAsync("corr_1")).Should().ContainSingle();
        (await repository.GetRecentAsync(10)).Should().ContainSingle();
        (await repository.SearchAsync(new WebhookTransformationRecommendationSearchRequestDto { CustomerId = "cust_1", EventType = "OrderCreated", RiskLevel = "Low" })).Should().ContainSingle();
        var indexes = AiMongoIndexInitializer.CreateWebhookTransformationRecommendationIndexModels();

        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<WebhookTransformationRecommendationResult>>(), It.IsAny<FindOptions<WebhookTransformationRecommendationResult, WebhookTransformationRecommendationResult>>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        indexes.Should().Contain(index => index.Options.Name == "idx_webhook_transformation_recommendations_event_id");
        indexes.Should().Contain(index => index.Options.Name == "idx_webhook_transformation_recommendations_customer_id");
        indexes.Should().Contain(index => index.Options.Name == "idx_webhook_transformation_recommendations_event_type");
        indexes.Should().Contain(index => index.Options.Name == "idx_webhook_transformation_recommendations_risk_level");
    }

    [Fact]
    public void DiRegistration_ResolvesAgentPromptBuilderRepositoryAndOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration());
        services.AddAiMongoOptions(BuildConfiguration());
        services.AddAiPromptServices();
        services.AddSingleton<ILocalLlmClient>(_ => Mock.Of<ILocalLlmClient>());
        services.AddWebhookTransformationRecommendationServices();
        services.AddAiMongoServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IWebhookTransformationRecommendationAgent>().Should().BeOfType<WebhookTransformationRecommendationAgent>();
        provider.GetRequiredService<IWebhookTransformationPromptBuilder>().Should().BeOfType<WebhookTransformationPromptBuilder>();
        provider.GetRequiredService<IWebhookTransformationRecommendationRepository>().Should().BeOfType<WebhookTransformationRecommendationRepository>();
        provider.GetRequiredService<IOptions<AiOptions>>().Value.Model.Should().Be("llama3-test");
        provider.GetRequiredService<IOptions<AiMongoOptions>>().Value.WebhookTransformationRecommendationResultsCollectionName.Should().NotBeNullOrWhiteSpace();
    }

    private static WebhookTransformationRecommendationAgent CreateAgent(
        string? aiResponse = null,
        LlmResponseResult? llmResult = null,
        bool enabled = true)
    {
        var llm = new Mock<ILocalLlmClient>();
        llm.Setup(client => client.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult ?? LlmResponseResult.Success(aiResponse ?? ValidAiJson(), 1));
        var options = Options.Create(new AiOptions { Enabled = enabled, Provider = "Ollama", Model = "llama3-test", MaxPromptPayloadLength = 4000, MaskSensitiveValues = true });
        return new WebhookTransformationRecommendationAgent(options, new WebhookTransformationPromptBuilder(options), llm.Object, NullLogger<WebhookTransformationRecommendationAgent>.Instance);
    }

    private static WebhookTransformationRecommendationRequestDto CreateRequest(
        object? sourcePayload = null,
        object? targetSamplePayload = null,
        object? targetSchema = null,
        object? existingMappingRules = null,
        IDictionary<string, string>? headers = null,
        string? targetUrl = "https://example.test/webhook") => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        EventType = "OrderCreated",
        Source = "Shopify",
        CustomerId = "cust_1",
        SourcePayload = sourcePayload ?? ReadPayload("order-created-source.json"),
        TargetSamplePayload = targetSamplePayload ?? ReadPayload("order-created-target.json"),
        TargetSchema = targetSchema,
        ExistingMappingRules = existingMappingRules,
        TargetUrl = targetUrl,
        Headers = headers ?? new Dictionary<string, string>(),
        ReceivedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 30, 0), DateTimeKind.Utc)
    };

    private static WebhookTransformationRecommendationRepository CreateRepository(IMongoCollection<WebhookTransformationRecommendationResult> collection)
    {
        var provider = new Mock<IWebhookTransformationRecommendationCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new WebhookTransformationRecommendationRepository(provider.Object);
    }

    private static Mock<IMongoCollection<WebhookTransformationRecommendationResult>> CreateCollectionReturning(IReadOnlyCollection<WebhookTransformationRecommendationResult> results)
    {
        var collection = new Mock<IMongoCollection<WebhookTransformationRecommendationResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<WebhookTransformationRecommendationResult>>(),
                It.IsAny<FindOptions<WebhookTransformationRecommendationResult, WebhookTransformationRecommendationResult>>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(CreateCursor(results).Object));
        return collection;

        static Mock<IAsyncCursor<WebhookTransformationRecommendationResult>> CreateCursor(IReadOnlyCollection<WebhookTransformationRecommendationResult> cursorResults)
        {
            var cursor = new Mock<IAsyncCursor<WebhookTransformationRecommendationResult>>();
            cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
            cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
            cursor.Setup(mongoCursor => mongoCursor.Current).Returns(cursorResults);
            return cursor;
        }
    }

    private static string ReadPayload(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "PayloadMapping", fileName);
        return File.Exists(path)
            ? File.ReadAllText(path)
            : File.ReadAllText(Path.Combine("TestData", "PayloadMapping", fileName));
    }

    private static string ValidAiJson() => """
        {"summary":"Mapped.","recommendedMappings":[{"sourceJsonPath":"$.orderId","targetJsonPath":"$.orderId","sourceFieldName":"orderId","targetFieldName":"orderId","transformationType":"DirectMap","transformationExpression":"orderId = orderId","isRequired":true,"confidenceScore":0.95}],"missingTargetFields":[],"unmappedSourceFields":[],"transformationNotes":[],"generatedTransformationCode":"// recommended code requires human review","confidenceScore":0.95,"riskLevel":"Low","generatedAtUtc":"2026-05-14T10:31:00Z"}
        """;

    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AiOptions.SectionName}:Enabled"] = "true",
            [$"{AiOptions.SectionName}:Provider"] = "Ollama",
            [$"{AiOptions.SectionName}:Model"] = "llama3-test",
            [$"{AiOptions.SectionName}:Endpoint"] = "http://localhost:11434",
            [$"{AiMongoOptions.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
            [$"{AiMongoOptions.SectionName}:DatabaseName"] = "hookbridge_ai_tests"
        }).Build();
}
