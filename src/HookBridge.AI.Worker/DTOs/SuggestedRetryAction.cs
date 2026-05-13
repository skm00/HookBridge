using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<SuggestedRetryAction>))]
public enum SuggestedRetryAction
{
    None,
    RetryImmediately,
    RetryWithBackoff,
    MoveToDeadLetter,
    PauseEndpoint,
    RequireManualReview
}
