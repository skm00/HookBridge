using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<SecurityAgentReasonCode>))]
public enum SecurityAgentReasonCode
{
    Unknown,
    SignatureValidationFailed,
    AuthenticationFailed,
    SuspiciousPayload,
    ScriptContentDetected,
    SqlInjectionPattern,
    CommandInjectionPattern,
    PathTraversalPattern,
    SecretValueDetected,
    LargePayload,
    SuspiciousUserAgent,
    DuplicateDetected,
    ReplayDetected,
    HighRiskSecurityFinding,
    CriticalSecurityFinding
}
