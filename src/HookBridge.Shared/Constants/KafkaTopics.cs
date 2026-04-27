namespace HookBridge.Shared.Constants;

/// <summary>
/// Kafka topic names used by HookBridge.
/// </summary>
public static class KafkaTopics
{
    public const string WebhookEvents = "webhook-events";

    public const string WebhookRetry = "webhook-retry";

    public const string WebhookDlq = "webhook-dlq";
}
