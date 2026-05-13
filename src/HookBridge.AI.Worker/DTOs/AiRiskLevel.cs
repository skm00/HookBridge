using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiRiskLevel>))]
public enum AiRiskLevel
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}
