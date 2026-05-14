namespace HookBridge.AI.Worker.DTOs;

public enum WebhookDuplicateReplayReason
{
    None,
    SameEventId,
    SameCorrelationId,
    SamePayloadHash,
    SameSignatureHash,
    EventTimestampTooOld,
    EventTimestampInFuture,
    SignatureTimestampExpired,
    HighFrequencyRepeat,
    Unknown
}
