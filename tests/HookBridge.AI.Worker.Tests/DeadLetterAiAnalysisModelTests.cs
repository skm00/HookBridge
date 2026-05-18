using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace HookBridge.AI.Worker.Tests;

public sealed class DeadLetterAiAnalysisModelTests
{
    [Fact]
    public void FromResponse_MapsRequestAndResponseFields_ToMongoResult()
    {
        var request = CreateRequest();
        var response = CreateResponse();

        var result = DeadLetterAiAnalysisResult.FromResponse(response, request);

        Assert.Equal(response.DeadLetterId, result.DeadLetterId);
        Assert.Equal(response.EventId, result.EventId);
        Assert.Equal(response.CorrelationId, result.CorrelationId);
        Assert.Equal(request.CustomerId, result.CustomerId);
        Assert.Equal(request.CustomerIdType, result.CustomerIdType);
        Assert.Equal(request.SubscriptionId, result.SubscriptionId);
        Assert.Equal(request.EndpointId, result.EndpointId);
        Assert.Equal(request.Environment, result.Environment);
        Assert.Equal(request.EventType, result.EventType);
        Assert.Equal(request.Source, result.Source);
        Assert.Equal(request.TargetUrl, result.TargetUrl);
        Assert.Equal(request.HttpMethod, result.HttpMethod);
        Assert.Equal(request.StatusCode, result.StatusCode);
        Assert.Equal(request.FailureReason, result.FailureReason);
        Assert.Equal(request.RetryCount, result.RetryCount);
        Assert.Equal(request.MaxRetryCount, result.MaxRetryCount);
        Assert.Equal(request.IsSuspicious, result.IsSuspicious);
        Assert.Equal(request.IsReplay, result.IsReplay);
        Assert.Equal(request.IsDuplicate, result.IsDuplicate);
        Assert.Equal(response.RootCause, result.RootCause);
        Assert.Equal(response.Summary, result.Summary);
        Assert.Equal(response.Recommendation, result.Recommendation);
        Assert.Equal(response.ReplaySafety, result.ReplaySafety);
        Assert.Equal(response.SuggestedAction, result.SuggestedAction);
        Assert.Equal(response.RiskLevel, result.RiskLevel);
        Assert.Equal(response.ConfidenceScore, result.ConfidenceScore);
        Assert.Equal(response.ConfidenceLevel, result.ConfidenceLevel);
        Assert.Equal(response.RequiresApproval, result.RequiresApproval);
        Assert.Equal(response.SafeModeDecision, result.SafeModeDecision);
        Assert.Equal(response.IsActionAllowed, result.IsActionAllowed);
        Assert.Equal(response.ReasonCodes, result.ReasonCodes);
        Assert.Equal(response.Model, result.Model);
        Assert.Equal(response.Provider, result.Provider);
        Assert.Equal(response.Fallback.UsedFallback, result.UsedFallback);
        Assert.Equal(response.PromptName, result.PromptName);
        Assert.Equal(response.PromptVersion, result.PromptVersion);
        Assert.Equal(response.PromptHash, result.PromptHash);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, result.GeneratedAtUtc.Kind);
    }

    [Fact]
    public void ToResponseDto_MapsMongoResult_ToApiResponse()
    {
        var result = DeadLetterAiAnalysisResult.FromResponse(CreateResponse(), CreateRequest());

        var dto = result.ToResponseDto();

        Assert.Equal(result.DeadLetterId, dto.DeadLetterId);
        Assert.Equal(result.EventId, dto.EventId);
        Assert.Equal(result.CorrelationId, dto.CorrelationId);
        Assert.Equal(result.RootCause, dto.RootCause);
        Assert.Equal(result.Summary, dto.Summary);
        Assert.Equal(result.Recommendation, dto.Recommendation);
        Assert.Equal(result.ReplaySafety, dto.ReplaySafety);
        Assert.Equal(result.SuggestedAction, dto.SuggestedAction);
        Assert.Equal(result.RiskLevel, dto.RiskLevel);
        Assert.Equal(result.ConfidenceScore, dto.ConfidenceScore);
        Assert.Equal(result.ConfidenceLevel, dto.ConfidenceLevel);
        Assert.Equal(result.RequiresApproval, dto.RequiresApproval);
        Assert.Equal(result.SafeModeDecision, dto.SafeModeDecision);
        Assert.Equal(result.IsActionAllowed, dto.IsActionAllowed);
        Assert.Equal(result.ReasonCodes, dto.ReasonCodes);
        Assert.Equal(result.GeneratedAtUtc, dto.GeneratedAtUtc);
        Assert.Equal(result.Model, dto.Model);
        Assert.Equal(result.Provider, dto.Provider);
        Assert.Equal(result.UsedFallback, dto.Fallback.UsedFallback);
        Assert.Equal(result.PromptName, dto.PromptName);
        Assert.Equal(result.PromptVersion, dto.PromptVersion);
        Assert.Equal(result.PromptHash, dto.PromptHash);
    }

    [Fact]
    public void DeadLetterAiAnalysisResult_SerializesEnumsAsStrings()
    {
        var result = DeadLetterAiAnalysisResult.FromResponse(CreateResponse(), CreateRequest());
        result.Id = ObjectId.GenerateNewId().ToString();

        var document = result.ToBsonDocument();
        var deserialized = BsonSerializer.Deserialize<DeadLetterAiAnalysisResult>(document);

        Assert.Equal("ReplayWithCaution", document[nameof(DeadLetterAiAnalysisResult.ReplaySafety)].AsString);
        Assert.Equal("ReplayWithBackoff", document[nameof(DeadLetterAiAnalysisResult.SuggestedAction)].AsString);
        Assert.Equal("RequiresApproval", document[nameof(DeadLetterAiAnalysisResult.SafeModeDecision)].AsString);
        Assert.Equal(DateTimeKind.Utc, deserialized.GeneratedAtUtc.Kind);
        Assert.Equal(result.ReplaySafety, deserialized.ReplaySafety);
    }

    [Fact]
    public void CreateDeadLetterAiAnalysisIndexModels_IncludesRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateDeadLetterAiAnalysisIndexModels();

        Assert.Equal(12, indexes.Count);
        Assert.Contains(indexes, index => index.Options?.Name == "idx_deadletter_ai_analysis_deadletter_id_unique" && index.Options.Unique == true);
        Assert.Contains(indexes, index => index.Options?.Name == "idx_deadletter_ai_analysis_event_id");
        Assert.Contains(indexes, index => index.Options?.Name == "idx_deadletter_ai_analysis_generated_at_utc_desc");
    }

    [Fact]
    public void KafkaAndMongoOptions_DefaultDeadLetterNamesAreConfigured()
    {
        var kafkaOptions = new AiKafkaOptions();
        var mongoOptions = new AiMongoOptions();

        Assert.Equal(AiKafkaTopics.DeadLetterAnalysis, kafkaOptions.DeadLetterAiAnalysisTopic);
        Assert.Equal("hookbridge.ai.deadletter-analysis", AiKafkaTopics.DeadLetterAnalysis);
        Assert.Equal(AiMongoOptions.DefaultDeadLetterAiAnalysisResultsCollectionName, mongoOptions.DeadLetterAiAnalysisResultsCollectionName);
        Assert.Equal("dead_letter_ai_analysis_results", AiMongoOptions.DefaultDeadLetterAiAnalysisResultsCollectionName);
    }

    private static DeadLetterAiAnalysisRequestDto CreateRequest() => new()
    {
        DeadLetterId = "dlq_1001",
        EventId = "evt_1001",
        CorrelationId = "corr_1001",
        CustomerId = "cust_123",
        CustomerIdType = "external",
        SubscriptionId = "sub_456",
        EndpointId = "endpoint_789",
        Environment = "qa",
        EventType = "WebhookDeliveryFailed",
        Source = "hookbridge.worker",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        StatusCode = 429,
        FailureReason = "Too Many Requests",
        RetryCount = 5,
        MaxRetryCount = 5,
        FailedAtUtc = DateTime.UtcNow.AddMinutes(-15),
        MovedToDeadLetterAtUtc = DateTime.UtcNow,
        IsSuspicious = true,
        IsReplay = true,
        IsDuplicate = true
    };

    private static DeadLetterAiAnalysisResponseDto CreateResponse() => new()
    {
        DeadLetterId = "dlq_1001",
        EventId = "evt_1001",
        CorrelationId = "corr_1001",
        RootCause = "Rate limit.",
        Summary = "Reached DLQ.",
        Recommendation = "Replay with backoff after approval.",
        ReplaySafety = DeadLetterReplaySafety.ReplayWithCaution,
        SuggestedAction = DeadLetterSuggestedAction.ReplayWithBackoff,
        RiskLevel = "Medium",
        ConfidenceScore = 0.82,
        ConfidenceLevel = AiConfidenceLevel.High,
        RequiresApproval = true,
        SafeModeDecision = AiSafeModeDecision.RequiresApproval,
        IsActionAllowed = false,
        ReasonCodes = [DeadLetterReasonCode.RateLimited, DeadLetterReasonCode.MaxRetryReached],
        GeneratedAtUtc = DateTime.UtcNow,
        Model = "test-model",
        Provider = "test-provider",
        Fallback = new AiFallbackMetadataDto { UsedFallback = true, FallbackReason = AiFallbackReason.InvalidJson, Provider = "test-provider", Model = "test-model", GeneratedAtUtc = DateTime.UtcNow },
        PromptName = "DeadLetterAiAnalysis",
        PromptVersion = "v1.0.0",
        PromptHash = "abc123"
    };
}
