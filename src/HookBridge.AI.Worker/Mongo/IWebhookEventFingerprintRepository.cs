namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookEventFingerprintRepository
{
    Task InsertAsync(WebhookEventFingerprint fingerprint, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPayloadHashAsync(string payloadHash, string? customerId = null, string? subscriptionId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySignatureHashAsync(string signatureHash, DateTime? receivedAfterUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEventFingerprint>> GetRecentByCustomerAsync(string customerId, DateTime receivedAfterUtc, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEventFingerprint>> SearchSimilarAsync(string? customerId, string? subscriptionId, string? endpointId, string? payloadHash, string? signatureHash, DateTime? receivedAfterUtc, int limit, CancellationToken cancellationToken = default);
}
