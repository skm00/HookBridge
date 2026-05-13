using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<EndpointHealthStatus>))]
public enum EndpointHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Critical
}
