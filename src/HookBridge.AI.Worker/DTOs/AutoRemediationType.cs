using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AutoRemediationType>))]
public enum AutoRemediationType
{
    None = 0,
    RetryTuning = 1,
    DeadLetterReview = 2,
    EndpointPauseRecommendation = 3,
    EndpointResumeRecommendation = 4,
    SecurityQuarantineRecommendation = 5,
    KafkaLagInvestigation = 6,
    MongoHealthInvestigation = 7,
    ConcurrencyReduction = 8,
    TimeoutAdjustment = 9,
    CredentialReview = 10,
    PayloadContractReview = 11,
    ManualReview = 12
}
