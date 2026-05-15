using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<SecurityAgentDecision>))]
public enum SecurityAgentDecision
{
    None,
    Allow,
    Monitor,
    RequireManualReview,
    Quarantine,
    Reject,
    BlockTemporarily
}
