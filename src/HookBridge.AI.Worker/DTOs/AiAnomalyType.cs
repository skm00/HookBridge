using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonUnknownAiAnomalyTypeConverter))]
public enum AiAnomalyType
{
    Unknown,
    FailureSpike,
    RetrySpike,
    DeadLetterSpike,
    RateLimitSpike,
    TimeoutSpike,
    ServerErrorSpike,
    ClientErrorSpike,
    AuthenticationFailureSpike,
    SignatureValidationSpike,
    SuspiciousPayloadSpike,
    LatencySpike
}
