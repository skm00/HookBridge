using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiOrchestrationRecommendedAction>))]
public enum AiOrchestrationRecommendedAction
{
    None,
    Allow,
    RetryWithBackoff,
    MoveToDeadLetter,
    RequireManualReview,
    Quarantine,
    Reject,
    GenerateTransformation,
    GenerateDtoAndValidation
}
