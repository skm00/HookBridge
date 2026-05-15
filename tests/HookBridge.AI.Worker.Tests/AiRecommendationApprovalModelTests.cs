using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiRecommendationApprovalModelTests
{
    [Fact]
    public void AiRecommendationApprovalDtos_RoundTripAllProperties()
    {
        var fromUtc = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = fromUtc.AddHours(1);
        var create = new AiRecommendationApprovalCreateRequestDto
        {
            RecommendationId = "rec_1001",
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            CustomerId = "cust_123",
            SubscriptionId = "sub_456",
            EndpointId = "endpoint_789",
            RecommendationType = AiRecommendationType.RetryRecommendation,
            RiskLevel = "High",
            SuggestedAction = "RetryWithBackoff",
            Summary = "Summary",
            Recommendation = "Recommendation",
            RequestedBy = "HookBridge.AI.Worker"
        };
        var search = new AiRecommendationApprovalSearchRequestDto
        {
            CustomerId = create.CustomerId,
            SubscriptionId = create.SubscriptionId,
            EndpointId = create.EndpointId,
            RecommendationType = AiRecommendationType.SecurityRecommendation,
            ApprovalStatus = AiRecommendationApprovalStatus.NeedsMoreInfo,
            RiskLevel = "Critical",
            FromUtc = fromUtc,
            ToUtc = toUtc,
            PageNumber = 2,
            PageSize = 25
        };
        var update = new AiRecommendationApprovalUpdateRequestDto
        {
            ApprovalStatus = AiRecommendationApprovalStatus.Approved,
            ReviewedBy = "admin@hookbridge.local",
            ReviewComment = "Approved."
        };
        var response = new AiRecommendationApprovalResponseDto
        {
            Id = "507f1f77bcf86cd799439011",
            RecommendationId = create.RecommendationId,
            EventId = create.EventId,
            CorrelationId = create.CorrelationId,
            CustomerId = create.CustomerId,
            SubscriptionId = create.SubscriptionId,
            EndpointId = create.EndpointId,
            RecommendationType = create.RecommendationType!.Value,
            ApprovalStatus = update.ApprovalStatus!.Value,
            RiskLevel = create.RiskLevel,
            SuggestedAction = create.SuggestedAction,
            Summary = create.Summary,
            Recommendation = create.Recommendation,
            RequestedBy = create.RequestedBy,
            ReviewedBy = update.ReviewedBy,
            ReviewComment = update.ReviewComment,
            RequiresApproval = true,
            CreatedAtUtc = fromUtc,
            ReviewedAtUtc = toUtc,
            AppliedAtUtc = toUtc.AddMinutes(1),
            ExpiresAtUtc = toUtc.AddHours(72)
        };

        create.RecommendationId.Should().Be("rec_1001");
        create.EventId.Should().Be("evt_12345");
        create.CorrelationId.Should().Be("corr_789");
        create.CustomerId.Should().Be("cust_123");
        create.SubscriptionId.Should().Be("sub_456");
        create.EndpointId.Should().Be("endpoint_789");
        create.RecommendationType.Should().Be(AiRecommendationType.RetryRecommendation);
        create.RiskLevel.Should().Be("High");
        create.SuggestedAction.Should().Be("RetryWithBackoff");
        create.Summary.Should().Be("Summary");
        create.Recommendation.Should().Be("Recommendation");
        create.RequestedBy.Should().Be("HookBridge.AI.Worker");

        search.CustomerId.Should().Be("cust_123");
        search.SubscriptionId.Should().Be("sub_456");
        search.EndpointId.Should().Be("endpoint_789");
        search.RecommendationType.Should().Be(AiRecommendationType.SecurityRecommendation);
        search.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.NeedsMoreInfo);
        search.RiskLevel.Should().Be("Critical");
        search.FromUtc.Should().Be(fromUtc);
        search.ToUtc.Should().Be(toUtc);
        search.PageNumber.Should().Be(2);
        search.PageSize.Should().Be(25);

        update.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Approved);
        update.ReviewedBy.Should().Be("admin@hookbridge.local");
        update.ReviewComment.Should().Be("Approved.");

        response.Id.Should().Be("507f1f77bcf86cd799439011");
        response.RecommendationId.Should().Be(create.RecommendationId);
        response.EventId.Should().Be(create.EventId);
        response.CorrelationId.Should().Be(create.CorrelationId);
        response.CustomerId.Should().Be(create.CustomerId);
        response.SubscriptionId.Should().Be(create.SubscriptionId);
        response.EndpointId.Should().Be(create.EndpointId);
        response.RecommendationType.Should().Be(AiRecommendationType.RetryRecommendation);
        response.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Approved);
        response.RiskLevel.Should().Be("High");
        response.SuggestedAction.Should().Be("RetryWithBackoff");
        response.Summary.Should().Be("Summary");
        response.Recommendation.Should().Be("Recommendation");
        response.RequestedBy.Should().Be("HookBridge.AI.Worker");
        response.ReviewedBy.Should().Be("admin@hookbridge.local");
        response.ReviewComment.Should().Be("Approved.");
        response.RequiresApproval.Should().BeTrue();
        response.CreatedAtUtc.Should().Be(fromUtc);
        response.ReviewedAtUtc.Should().Be(toUtc);
        response.AppliedAtUtc.Should().Be(toUtc.AddMinutes(1));
        response.ExpiresAtUtc.Should().Be(toUtc.AddHours(72));
    }

    [Fact]
    public void AiRecommendationApprovalEntity_RoundTripsAllProperties()
    {
        var createdAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var approval = new AiRecommendationApproval
        {
            Id = "507f1f77bcf86cd799439011",
            RecommendationId = "rec_1001",
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            CustomerId = "cust_123",
            SubscriptionId = "sub_456",
            EndpointId = "endpoint_789",
            RecommendationType = AiRecommendationType.TransformationRecommendation,
            ApprovalStatus = AiRecommendationApprovalStatus.PendingReview,
            RiskLevel = "Critical",
            SuggestedAction = "ReviewTransformation",
            Summary = "Summary",
            Recommendation = "Recommendation",
            RequestedBy = "worker",
            ReviewedBy = "admin",
            ReviewComment = "Needs review",
            CreatedAtUtc = createdAt,
            ReviewedAtUtc = createdAt.AddMinutes(5),
            AppliedAtUtc = createdAt.AddMinutes(10),
            ExpiresAtUtc = createdAt.AddHours(72)
        };

        approval.Id.Should().Be("507f1f77bcf86cd799439011");
        approval.RecommendationId.Should().Be("rec_1001");
        approval.EventId.Should().Be("evt_12345");
        approval.CorrelationId.Should().Be("corr_789");
        approval.CustomerId.Should().Be("cust_123");
        approval.SubscriptionId.Should().Be("sub_456");
        approval.EndpointId.Should().Be("endpoint_789");
        approval.RecommendationType.Should().Be(AiRecommendationType.TransformationRecommendation);
        approval.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.PendingReview);
        approval.RiskLevel.Should().Be("Critical");
        approval.SuggestedAction.Should().Be("ReviewTransformation");
        approval.Summary.Should().Be("Summary");
        approval.Recommendation.Should().Be("Recommendation");
        approval.RequestedBy.Should().Be("worker");
        approval.ReviewedBy.Should().Be("admin");
        approval.ReviewComment.Should().Be("Needs review");
        approval.CreatedAtUtc.Should().Be(createdAt);
        approval.ReviewedAtUtc.Should().Be(createdAt.AddMinutes(5));
        approval.AppliedAtUtc.Should().Be(createdAt.AddMinutes(10));
        approval.ExpiresAtUtc.Should().Be(createdAt.AddHours(72));
    }

    [Fact]
    public void ExistingAiResultDocuments_DefaultApprovalMetadataIsAdvisory()
    {
        object[] results =
        [
            new AiAnalysisResult(),
            new AiAnomalyRecord(),
            new AiSecurityAnalysisResult(),
            new CustomerEndpointRiskScoreResult(),
            new FluentValidationRuleGenerationResult(),
            new JsonToDtoSuggestionResult(),
            new PayloadSchemaDetectionResult(),
            new WebhookFailureAnomalyDetectionResult(),
            new WebhookTransformationRecommendationResult()
        ];

        foreach (var result in results)
        {
            var type = result.GetType();
            type.GetProperty("ApprovalStatus")!.GetValue(result).Should().Be(AiRecommendationApprovalStatus.PendingReview, type.Name);
            type.GetProperty("ApprovalId")!.GetValue(result).Should().BeNull(type.Name);
            type.GetProperty("RequiresApproval")!.GetValue(result).Should().Be(true, type.Name);
        }
    }

    [Fact]
    public void ApprovalEnums_ExposeAllWorkflowValues()
    {
        Enum.GetNames<AiRecommendationApprovalStatus>().Should().BeEquivalentTo(
            "PendingReview",
            "Approved",
            "Rejected",
            "NeedsMoreInfo",
            "Applied",
            "Expired");

        Enum.GetNames<AiRecommendationType>().Should().BeEquivalentTo(
            "RetryRecommendation",
            "DeadLetterRecommendation",
            "EndpointRiskRecommendation",
            "SecurityRecommendation",
            "TransformationRecommendation",
            "ValidationRuleRecommendation",
            "DtoSuggestion",
            "AnomalyRecommendation",
            "LogSummaryRecommendation",
            "NaturalLanguageRecommendation");
    }

    [Fact]
    public void AiMongoOptions_DefaultApprovalCollectionNameIsConfigured()
    {
        var options = new AiMongoOptions();

        AiMongoOptions.DefaultAiRecommendationApprovalsCollectionName.Should().Be("ai_recommendation_approvals");
        options.AiRecommendationApprovalsCollectionName.Should().Be("ai_recommendation_approvals");
    }
}
