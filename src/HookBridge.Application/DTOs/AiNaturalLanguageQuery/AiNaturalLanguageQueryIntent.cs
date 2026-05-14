using System.Text.Json.Serialization;

namespace HookBridge.Application.DTOs.AiNaturalLanguageQuery;

/// <summary>
/// Supported safe natural language query intents for AI-assisted webhook investigation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiNaturalLanguageQueryIntent
{
    Unknown,
    FailureAnalysis,
    AnomalySearch,
    SecurityFindings,
    RetryRecommendations,
    EndpointRisk,
    EndpointHealth,
    EventLookup,
    DashboardSummary,
    DeadLetterRecommendations
}
