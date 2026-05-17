using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AutoRemediationReasonCode>))]
public enum AutoRemediationReasonCode
{
    Unknown = 0,
    RateLimited = 1,
    Timeout = 2,
    ServerError = 3,
    ClientError = 4,
    AuthenticationFailure = 5,
    AuthorizationFailure = 6,
    MaxRetryReached = 7,
    DeadLetterRecordsFound = 8,
    HighKafkaLag = 9,
    MongoUnhealthy = 10,
    MongoHighLatency = 11,
    SuspiciousPayload = 12,
    ReplayDetected = 13,
    DuplicateDetected = 14,
    EndpointHighRisk = 15,
    EndpointCriticalRisk = 16,
    LowConfidence = 17,
    HumanApprovalRequired = 18
}
