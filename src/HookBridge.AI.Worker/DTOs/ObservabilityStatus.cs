using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<ObservabilityStatus>))]
public enum ObservabilityStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Critical
}
