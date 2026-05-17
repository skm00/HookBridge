using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<AiSafeModeDecision>))]
public enum AiSafeModeDecision
{
    Allowed,
    Blocked,
    RequiresApproval,
    RequiresManualReview
}
