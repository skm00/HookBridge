using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<RetryAgentReasonCode>))]
public enum RetryAgentReasonCode
{
    Unknown,
    RateLimited,
    Timeout,
    ServerError,
    ClientError,
    AuthenticationFailure,
    AuthorizationFailure,
    NotFound,
    MaxRetryReached,
    EndpointHighRisk,
    EndpointCriticalRisk,
    LargePayload,
    DuplicateDetected,
    ReplayDetected,
    ManualReviewRequired
}
