using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<SuggestedValidationSeverity>))]
public enum SuggestedValidationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
