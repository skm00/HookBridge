using System.Text.Json.Serialization;

namespace HookBridge.AI.Worker.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter<ObservabilitySuggestedAction>))]
public enum ObservabilitySuggestedAction
{
    None,
    Monitor,
    InvestigateLogs,
    CheckKafkaLag,
    CheckMongoHealth,
    ReduceWebhookConcurrency,
    ReviewDeadLetterQueue,
    ReviewSecurityFindings,
    EscalateToSupport,
    RequireManualReview
}
