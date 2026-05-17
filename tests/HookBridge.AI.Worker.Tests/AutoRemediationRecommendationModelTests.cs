using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Tests;

public sealed class AutoRemediationRecommendationModelTests
{
    [Fact]
    public void FromResponse_MapsRequestAndResponseFields_ToMongoResult()
    {
        var createdAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5), DateTimeKind.Unspecified);
        var generatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var request = CreateRequest(createdAt);
        var response = CreateResponse(generatedAt);

        var result = AutoRemediationRecommendationResult.FromResponse(response, request);

        Assert.Equal(response.EventId, result.EventId);
        Assert.Equal(response.CorrelationId, result.CorrelationId);
        Assert.Equal(request.CustomerId, result.CustomerId);
        Assert.Equal(request.CustomerIdType, result.CustomerIdType);
        Assert.Equal(request.SubscriptionId, result.SubscriptionId);
        Assert.Equal(request.EndpointId, result.EndpointId);
        Assert.Equal(request.Environment, result.Environment);
        Assert.Equal(request.Source, result.Source);
        Assert.Equal(request.EventType, result.EventType);
        Assert.Equal(response.RiskLevel, result.RiskLevel);
        Assert.Equal(response.ConfidenceScore, result.ConfidenceScore);
        Assert.Equal(request.FailureReason, result.FailureReason);
        Assert.Equal(request.StatusCode, result.StatusCode);
        Assert.Equal(request.RetryCount, result.RetryCount);
        Assert.Equal(request.MaxRetryCount, result.MaxRetryCount);
        Assert.Equal(request.DeadLetterCount, result.DeadLetterCount);
        Assert.Equal(request.KafkaConsumerLag, result.KafkaConsumerLag);
        Assert.Equal(request.MongoIsHealthy, result.MongoIsHealthy);
        Assert.Equal(request.MongoLatencyMs, result.MongoLatencyMs);
        Assert.Equal(request.IsSuspicious, result.IsSuspicious);
        Assert.Equal(request.IsReplay, result.IsReplay);
        Assert.Equal(request.IsDuplicate, result.IsDuplicate);
        Assert.Equal(request.EndpointHealthStatus, result.EndpointHealthStatus);
        Assert.Equal(request.ObservabilityStatus, result.ObservabilityStatus);
        Assert.Equal(request.SecurityDecision, result.SecurityDecision);
        Assert.Equal(request.RetryDecision, result.RetryDecision);
        Assert.Equal(response.RemediationType, result.RemediationType);
        Assert.Equal(response.RecommendedAction, result.RecommendedAction);
        Assert.Equal(response.RequiresApproval, result.RequiresApproval);
        Assert.Equal(response.CanAutoApply, result.CanAutoApply);
        Assert.Equal(response.Summary, result.Summary);
        Assert.Equal(response.Recommendation, result.Recommendation);
        Assert.Equal(response.Steps, result.Steps);
        Assert.Equal(response.ReasonCodes, result.ReasonCodes);
        Assert.Equal(DateTimeKind.Utc, result.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, result.GeneratedAtUtc.Kind);
    }

    [Fact]
    public void ToResponseDto_MapsMongoResult_ToApiResponse()
    {
        var result = AutoRemediationRecommendationResult.FromResponse(CreateResponse(DateTime.UtcNow), CreateRequest(DateTime.UtcNow));

        var dto = result.ToResponseDto();

        Assert.Equal(result.EventId, dto.EventId);
        Assert.Equal(result.CorrelationId, dto.CorrelationId);
        Assert.Equal(result.RemediationType, dto.RemediationType);
        Assert.Equal(result.RecommendedAction, dto.RecommendedAction);
        Assert.Equal(result.RiskLevel, dto.RiskLevel);
        Assert.Equal(result.ConfidenceScore, dto.ConfidenceScore);
        Assert.Equal(result.RequiresApproval, dto.RequiresApproval);
        Assert.Equal(result.CanAutoApply, dto.CanAutoApply);
        Assert.Equal(result.Summary, dto.Summary);
        Assert.Equal(result.Recommendation, dto.Recommendation);
        Assert.Equal(result.Steps, dto.Steps);
        Assert.Equal(result.ReasonCodes, dto.ReasonCodes);
        Assert.Equal(result.GeneratedAtUtc, dto.GeneratedAtUtc);
    }

    [Fact]
    public void RequestValidation_ReturnsExpectedFailure_ForWhitespaceEventId()
    {
        var request = new AutoRemediationRecommendationRequestDto
        {
            EventId = " ",
            ConfidenceScore = 0.8,
            StatusCode = 500,
            RetryCount = 1,
            MaxRetryCount = 3,
            CreatedAtUtc = DateTime.UtcNow
        };

        var validationResults = Validate(request);

        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(request.EventId)) &&
            result.ErrorMessage == "The EventId field is required.");
    }

    [Fact]
    public void RequestValidation_ReturnsExpectedFailures_ForInvalidValues()
    {
        var request = new AutoRemediationRecommendationRequestDto
        {
            EventId = "evt_invalid_values",
            ConfidenceScore = 1.1,
            StatusCode = 600,
            RetryCount = -1,
            MaxRetryCount = -1,
            DeadLetterCount = -1,
            KafkaConsumerLag = -1,
            MongoLatencyMs = -1,
            CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var validationResults = Validate(request);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.ConfidenceScore)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.CreatedAtUtc)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.StatusCode)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.RetryCount)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.MaxRetryCount)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.DeadLetterCount)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.KafkaConsumerLag)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(request.MongoLatencyMs)));
    }

    [Fact]
    public void ResponseValidation_ReturnsExpectedFailures_ForInvalidValues()
    {
        var response = new AutoRemediationRecommendationResponseDto
        {
            ConfidenceScore = -0.1,
            GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        };

        var validationResults = Validate(response);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(response.ConfidenceScore)));
        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(response.GeneratedAtUtc)));
    }

    [Fact]
    public void Options_DefaultsMatchAutoRemediationConfiguration()
    {
        var options = new AutoRemediationRecommendationOptions();
        var kafkaOptions = new AiKafkaOptions();
        var mongoOptions = new AiMongoOptions();

        Assert.True(options.Enabled);
        Assert.False(options.AllowAutoApplyLowRisk);
        Assert.True(options.RequireApprovalForHighRisk);
        Assert.True(options.RequireApprovalForCriticalRisk);
        Assert.True(options.RequireApprovalForSecurityActions);
        Assert.True(options.RequireApprovalForEndpointPause);
        Assert.True(options.RequireApprovalForDeadLetterActions);
        Assert.Equal(0.60, options.LowConfidenceThreshold);
        Assert.Equal(1000, options.KafkaLagThreshold);
        Assert.Equal(1000, options.MongoLatencyThresholdMs);
        Assert.Equal(AiKafkaTopics.AutoRemediation, kafkaOptions.AutoRemediationTopic);
        Assert.Equal(AiMongoOptions.DefaultAutoRemediationRecommendationResultsCollectionName, mongoOptions.AutoRemediationRecommendationResultsCollectionName);
    }

    private static List<ValidationResult> Validate(object value)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), validationResults, validateAllProperties: true);
        return validationResults;
    }

    private static AutoRemediationRecommendationRequestDto CreateRequest(DateTime createdAtUtc) => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        CustomerIdType = "external",
        SubscriptionId = "sub_1",
        EndpointId = "endpoint_1",
        Environment = "qa",
        Source = "worker",
        EventType = "WebhookDeliveryFailed",
        RiskLevel = "High",
        ConfidenceScore = 0.72,
        FailureReason = "Too Many Requests",
        StatusCode = 429,
        RetryCount = 2,
        MaxRetryCount = 5,
        DeadLetterCount = 1,
        KafkaConsumerLag = 1234,
        MongoIsHealthy = false,
        MongoLatencyMs = 1500,
        IsSuspicious = true,
        IsReplay = true,
        IsDuplicate = true,
        EndpointHealthStatus = "Critical",
        ObservabilityStatus = "Critical",
        SecurityDecision = "QuarantineEvent",
        RetryDecision = "RetryWithBackoff",
        CreatedAtUtc = createdAtUtc
    };

    private static AutoRemediationRecommendationResponseDto CreateResponse(DateTime generatedAtUtc) => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        RemediationType = AutoRemediationType.RetryTuning,
        RecommendedAction = AutoRemediationRecommendedAction.RetryWithBackoff,
        RiskLevel = "High",
        ConfidenceScore = 0.72,
        RequiresApproval = true,
        CanAutoApply = false,
        Summary = "Rate limited.",
        Recommendation = "Retry with backoff.",
        Steps = ["Keep in retry queue.", "Apply backoff."],
        ReasonCodes = [AutoRemediationReasonCode.RateLimited, AutoRemediationReasonCode.HumanApprovalRequired],
        GeneratedAtUtc = generatedAtUtc
    };
}
