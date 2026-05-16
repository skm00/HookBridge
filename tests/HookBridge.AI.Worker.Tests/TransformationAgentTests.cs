using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.TransformationAgent;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class TransformationAgentTests
{
    [Fact]
    public void TransformationAgentOptions_DefaultsMatchSafetyConfiguration()
    {
        var options = new TransformationAgentOptions();
        options.Enabled.Should().BeTrue();
        options.MinimumReadyConfidenceScore.Should().Be(0.80);
        options.MinimumReviewConfidenceScore.Should().Be(0.60);
        options.RequireApprovalForGeneratedCode.Should().BeTrue();
        options.RequireApprovalForHighRisk.Should().BeTrue();
        options.RequireApprovalForCriticalRisk.Should().BeTrue();
        options.MaxPayloadLength.Should().Be(4000);
        options.MaxSchemaLength.Should().Be(4000);
    }

    [Fact]
    public async Task ExactFieldMapping_ReturnsMappingReady()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"id":"1","status":"created"}""", """{"id":"string","status":"string"}"""));
        response.TransformationDecision.Should().Be(TransformationAgentDecision.MappingReady);
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.DirectMappingAvailable);
    }

    [Fact]
    public async Task CaseInsensitiveMapping_Works()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"OrderId":"1"}""", """{"orderid":"string"}"""));
        response.RecommendedMappings.Should().Contain(mapping => mapping.SourceFieldName == "OrderId" && mapping.TargetFieldName == "orderid");
    }

    [Fact]
    public async Task SnakeCaseToCamelCaseMapping_Works()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"order_id":"1"}""", """{"orderId":"string"}"""));
        response.RecommendedMappings.Should().Contain(mapping => mapping.SourceFieldName == "order_id" && mapping.TargetFieldName == "orderId");
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.RenameMappingAvailable);
    }

    [Fact]
    public async Task CommonAliasMapping_Works()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"state":"paid","totalAmount":42}""", """{"status":"string","amount":0}"""));
        response.MissingTargetFields.Should().BeEmpty();
        response.RecommendedMappings.Should().HaveCount(2);
    }

    [Fact]
    public async Task MissingRequiredTargetFields_ReturnsMissingRequiredFields()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"id":"1"}""", """{"id":"string","amount":0}"""));
        response.TransformationDecision.Should().Be(TransformationAgentDecision.MissingRequiredFields);
        response.MissingTargetFields.Should().Contain("$.amount");
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidSourceJson_ReturnsInvalidSourcePayload()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("{not-json", """{"id":"string"}"""));
        response.TransformationDecision.Should().Be(TransformationAgentDecision.InvalidSourcePayload);
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.InvalidSourceJson);
    }

    [Fact]
    public async Task InvalidTargetJson_ReturnsInvalidTargetSchema()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"id":"1"}""", "{not-json"));
        response.TransformationDecision.Should().Be(TransformationAgentDecision.InvalidTargetSchema);
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.InvalidTargetJson);
    }

    [Fact]
    public async Task LowConfidence_ReturnsMappingNeedsReview()
    {
        var agent = CreateAiAgent(new WebhookTransformationRecommendationResponseDto { ConfidenceScore = 0.5, RiskLevel = "Low", RecommendedMappings = [Mapping()] });
        var response = await agent.AnalyzeAsync(CreateRequest());
        response.TransformationDecision.Should().Be(TransformationAgentDecision.MappingNeedsReview);
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.LowConfidenceMapping);
    }

    [Fact]
    public async Task GeneratedCode_RequiresApproval()
    {
        var agent = CreateAiAgent(new WebhookTransformationRecommendationResponseDto { ConfidenceScore = 0.9, RiskLevel = "Low", RecommendedMappings = [Mapping()], GeneratedTransformationCode = "target.id = source.id;" });
        var response = await agent.AnalyzeAsync(CreateRequest());
        response.RequiresApproval.Should().BeTrue();
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.GeneratedCodeRequiresApproval);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public async Task HighOrCriticalRisk_RequiresApproval(string riskLevel)
    {
        var agent = CreateAiAgent(new WebhookTransformationRecommendationResponseDto { ConfidenceScore = 0.9, RiskLevel = riskLevel, RecommendedMappings = [Mapping()] });
        var response = await agent.AnalyzeAsync(CreateRequest());
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task UnmappedSourceFieldsAndReasonCodes_ArePopulated()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest("""{"id":"1","customerId":"c1"}""", """{"id":"string"}"""));
        response.UnmappedSourceFields.Should().Contain("$.customerId");
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.UnmappedImportantSourceField);
    }

    [Fact]
    public async Task ConfidenceScore_IsClampedAndGeneratedAtUtcIsUtcAndPromptMetadataMapped()
    {
        var agent = CreateAiAgent(new WebhookTransformationRecommendationResponseDto { ConfidenceScore = 2, RiskLevel = "Low", RecommendedMappings = [Mapping()], GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), PromptName = "WebhookTransformationRecommendation", PromptVersion = "v1.0.0", PromptHash = "sha256:abc" });
        var response = await agent.AnalyzeAsync(CreateRequest());
        response.ConfidenceScore.Should().Be(1);
        response.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.PromptName.Should().Be("WebhookTransformationRecommendation");
        response.PromptVersion.Should().Be("v1.0.0");
        response.PromptHash.Should().Be("sha256:abc");
    }



    [Fact]
    public async Task TargetSchemaProperties_AreUsedWhenSamplePayloadIsMissing()
    {
        var request = CreateRequest("""{"id":"1","status":"created"}""", """{}""");
        request.TargetSamplePayload = null;
        request.TargetSchema = """{"properties":{"id":{"type":"string"},"status":{"type":"string"}}}""";

        var response = await CreateFallbackAgent().AnalyzeAsync(request);

        response.TransformationDecision.Should().Be(TransformationAgentDecision.MappingReady);
        response.MissingTargetFields.Should().BeEmpty();
        response.RecommendedMappings.Should().HaveCount(2);
    }

    [Fact]
    public async Task NestedArrayPayload_FallbackMapsNestedFields()
    {
        var response = await CreateFallbackAgent().AnalyzeAsync(CreateRequest(
            """{"items":[{"order_id":"ORD-1"}]}""",
            """{"items":[{"orderId":"string"}]}"""));

        response.RecommendedMappings.Should().Contain(mapping => mapping.SourceJsonPath == "$.items[].order_id" && mapping.TargetJsonPath == "$.items[].orderId");
        response.TransformationDecision.Should().Be(TransformationAgentDecision.MappingReady);
    }

    [Fact]
    public async Task AiUnavailable_UsesDeterministicFallback()
    {
        var response = await CreateAiAgent(null).AnalyzeAsync(CreateRequest("""{"id":"1"}""", """{"id":"string"}"""));

        response.Fallback.Should().BeTrue();
        response.TransformationDecision.Should().Be(TransformationAgentDecision.MappingReady);
    }

    [Fact]
    public async Task AiRecommendation_TypeAndDateConversionsPopulateReasonCodesAndSecretCodeIsSanitized()
    {
        var agent = CreateAiAgent(new WebhookTransformationRecommendationResponseDto
        {
            ConfidenceScore = 0.9,
            RiskLevel = "not-a-risk",
            RecommendedMappings =
            [
                Mapping(WebhookTransformationType.TypeConversion),
                Mapping(WebhookTransformationType.DateFormat, sourceFieldName: "created_at", targetFieldName: "createdAtUtc")
            ],
            GeneratedTransformationCode = "var secret = \"super-secret\"; var token=\"abc123\";"
        });

        var response = await agent.AnalyzeAsync(CreateRequest("""{"id":"1","created_at":"2026-05-14T10:30:00Z"}""", """{"id":"string","createdAtUtc":"datetime"}"""));

        response.RiskLevel.Should().Be("Unknown");
        response.GeneratedTransformationCode.Should().Contain("secret=\"***\"");
        response.GeneratedTransformationCode.Should().Contain("token=\"***\"");
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.TypeConversionRequired);
        response.ReasonCodes.Should().Contain(TransformationAgentReasonCode.DateFormatConversionRequired);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task MissingEventId_ThrowsValidationException()
    {
        var request = CreateRequest();
        request.EventId = string.Empty;

        var act = async () => await CreateFallbackAgent().AnalyzeAsync(request);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("EventId is required.");
    }

    [Fact]
    public async Task MissingTarget_ReturnsInvalidTargetSchema()
    {
        var request = CreateRequest();
        request.TargetSamplePayload = null;
        request.TargetSchema = null;

        var response = await CreateFallbackAgent().AnalyzeAsync(request);

        response.TransformationDecision.Should().Be(TransformationAgentDecision.InvalidTargetSchema);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void ResponseValidation_RejectsNonUtcGeneratedAtAndOutOfRangeConfidence()
    {
        var response = new TransformationAgentResponseDto
        {
            GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
            ConfidenceScore = 1.1
        };

        var results = response.Validate(new ValidationContext(response)).ToList();

        results.Should().Contain(result => result.ErrorMessage == "GeneratedAtUtc must be UTC.");
        results.Should().Contain(result => result.ErrorMessage == "ConfidenceScore must be between 0 and 1.");
    }

    [Fact]
    public void RequestValidation_RejectsNonUtcReceivedAt()
    {
        var request = CreateRequest();
        request.ReceivedAtUtc = DateTime.Now;
        request.Validate(new ValidationContext(request)).Should().Contain(result => result.ErrorMessage == "ReceivedAtUtc must be UTC.");
    }

    private static TransformationAgent CreateFallbackAgent() => CreateAiAgent(null, new TransformationAgentOptions { Enabled = false });

    private static TransformationAgent CreateAiAgent(WebhookTransformationRecommendationResponseDto? aiResponse, TransformationAgentOptions? options = null)
        => new(Options.Create(options ?? new TransformationAgentOptions()), new StubRecommendationAgent(aiResponse), NullLogger<TransformationAgent>.Instance);

    private static TransformationAgentRequestDto CreateRequest(string source = """{"id":"1"}""", string target = """{"id":"string"}""") => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SourcePayload = source,
        TargetSamplePayload = target,
        ReceivedAtUtc = DateTime.UtcNow
    };

    private static WebhookFieldMappingRecommendationDto Mapping(WebhookTransformationType transformationType = WebhookTransformationType.DirectMap, string sourceFieldName = "id", string targetFieldName = "id") => new()
    {
        SourceJsonPath = "$." + sourceFieldName,
        TargetJsonPath = "$." + targetFieldName,
        SourceFieldName = sourceFieldName,
        TargetFieldName = targetFieldName,
        TransformationType = transformationType,
        ConfidenceScore = 0.9
    };

    private sealed class StubRecommendationAgent : IWebhookTransformationRecommendationAgent
    {
        private readonly WebhookTransformationRecommendationResponseDto? _response;
        public StubRecommendationAgent(WebhookTransformationRecommendationResponseDto? response) => _response = response;
        public Task<WebhookTransformationRecommendationResponseDto> RecommendAsync(WebhookTransformationRecommendationRequestDto request, CancellationToken cancellationToken = default)
            => _response is null ? throw new InvalidOperationException("unavailable") : Task.FromResult(new WebhookTransformationRecommendationResponseDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                Summary = _response.Summary,
                RecommendedMappings = _response.RecommendedMappings,
                MissingTargetFields = _response.MissingTargetFields,
                UnmappedSourceFields = _response.UnmappedSourceFields,
                GeneratedTransformationCode = _response.GeneratedTransformationCode,
                ConfidenceScore = _response.ConfidenceScore,
                RiskLevel = _response.RiskLevel,
                GeneratedAtUtc = _response.GeneratedAtUtc,
                Fallback = _response.Fallback,
                PromptName = _response.PromptName,
                PromptVersion = _response.PromptVersion,
                PromptHash = _response.PromptHash
            });
    }
}
