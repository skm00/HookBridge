using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<RetryAgentDecision>))]
public enum RetryAgentDecision
{
    None,
    RetryImmediately,
    RetryWithFixedDelay,
    RetryWithExponentialBackoff,
    MoveToDeadLetter,
    PauseEndpoint,
    RequireManualReview,
    DoNotRetry
}
