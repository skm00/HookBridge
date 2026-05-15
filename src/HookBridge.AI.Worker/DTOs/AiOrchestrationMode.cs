using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiOrchestrationMode>))]
public enum AiOrchestrationMode
{
    Sequential,
    Parallel
}
