using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<DeadLetterSuggestedAction>))]
public enum DeadLetterSuggestedAction
{
    None = 0,
    Replay,
    ReplayWithBackoff,
    FixPayloadBeforeReplay,
    FixAuthenticationBeforeReplay,
    FixEndpointBeforeReplay,
    KeepInDeadLetter,
    Quarantine,
    Reject,
    RequireManualReview
}
