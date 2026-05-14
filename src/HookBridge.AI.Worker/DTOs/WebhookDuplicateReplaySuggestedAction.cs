namespace HookBridge.AI.Worker.DTOs;

public enum WebhookDuplicateReplaySuggestedAction
{
    None,
    Allow,
    Monitor,
    IgnoreDuplicate,
    RequireManualReview,
    Quarantine,
    Reject
}
