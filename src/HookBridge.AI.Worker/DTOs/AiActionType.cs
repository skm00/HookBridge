using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiActionType>))]
public enum AiActionType
{
    Unknown,
    ReadOnlyQuery,
    GenerateRecommendation,
    RetryWebhook,
    MoveToDeadLetter,
    ReplayDeadLetter,
    PauseEndpoint,
    ResumeEndpoint,
    QuarantineEvent,
    RejectEvent,
    ApplyTransformation,
    ApplyValidationRule,
    UpdateConfiguration,
    ScaleWorker,
    RestartWorker,
    NotifyOnly
}
