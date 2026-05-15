using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiRecommendationApprovalStatus>))]
public enum AiRecommendationApprovalStatus
{
    PendingReview = 0,
    Approved = 1,
    Rejected = 2,
    NeedsMoreInfo = 3,
    Applied = 4,
    Expired = 5
}
