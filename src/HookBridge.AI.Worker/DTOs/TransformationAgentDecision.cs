using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<TransformationAgentDecision>))]
public enum TransformationAgentDecision
{
    None,
    MappingReady,
    MappingNeedsReview,
    MissingRequiredFields,
    InvalidSourcePayload,
    InvalidTargetSchema,
    RequireManualReview,
    RejectTransformation
}
