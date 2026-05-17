using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AutoRemediationRecommendedAction>))]
public enum AutoRemediationRecommendedAction
{
    None = 0,
    RetryWithBackoff = 1,
    ReduceConcurrency = 2,
    PauseEndpoint = 3,
    ResumeEndpoint = 4,
    MoveToDeadLetter = 5,
    ReviewDeadLetterQueue = 6,
    QuarantineEvent = 7,
    ReviewCredentials = 8,
    ReviewPayloadContract = 9,
    CheckKafkaConsumers = 10,
    CheckMongoHealth = 11,
    IncreaseTimeout = 12,
    RequireManualReview = 13,
    EscalateToSupport = 14
}
