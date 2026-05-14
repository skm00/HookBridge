using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiSecuritySuggestedAction
{
    None,
    Allow,
    Monitor,
    RequireManualReview,
    Quarantine,
    BlockTemporarily,
    Reject
}
