using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<DeadLetterReplaySafety>))]
public enum DeadLetterReplaySafety
{
    Unknown = 0,
    SafeToReplay,
    ReplayWithCaution,
    RequiresFixBeforeReplay,
    DoNotReplay,
    RequiresManualReview
}
