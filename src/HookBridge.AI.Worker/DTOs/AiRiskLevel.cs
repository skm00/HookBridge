using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonUnknownAiRiskLevelConverter))]
public enum AiRiskLevel
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}
