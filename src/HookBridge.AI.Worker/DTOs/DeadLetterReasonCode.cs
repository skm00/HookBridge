using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<DeadLetterReasonCode>))]
public enum DeadLetterReasonCode
{
    Unknown = 0,
    MaxRetryReached,
    RateLimited,
    Timeout,
    ServerError,
    ClientError,
    AuthenticationFailure,
    AuthorizationFailure,
    NotFound,
    PayloadContractIssue,
    SuspiciousPayload,
    ReplayDetected,
    DuplicateDetected,
    EndpointHighRisk,
    LowConfidence,
    ManualReviewRequired
}
