using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiFallbackReason>))]
public enum AiFallbackReason
{
    None,
    AiDisabled,
    ProviderUnavailable,
    ModelUnavailable,
    Timeout,
    InvalidResponse,
    InvalidJson,
    ConfigurationError,
    UnknownError
}
