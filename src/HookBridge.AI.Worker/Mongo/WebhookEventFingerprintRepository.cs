using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookEventFingerprintRepository : IWebhookEventFingerprintRepository
{
    private readonly IMongoCollection<WebhookEventFingerprint> _collection;

    public WebhookEventFingerprintRepository(IWebhookEventFingerprintCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public WebhookEventFingerprintRepository(IMongoCollection<WebhookEventFingerprint> collection) => _collection = collection;

    public Task InsertAsync(WebhookEventFingerprint fingerprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        fingerprint.ReceivedAtUtc = EnsureUtc(fingerprint.ReceivedAtUtc);
        fingerprint.CreatedAtUtc = EnsureUtc(fingerprint.CreatedAtUtc);
        fingerprint.ExpiresAtUtc = EnsureUtc(fingerprint.ExpiresAtUtc);
        if (fingerprint.EventTimestampUtc is not null) fingerprint.EventTimestampUtc = EnsureUtc(fingerprint.EventTimestampUtc.Value);
        return _collection.InsertOneAsync(fingerprint, cancellationToken: cancellationToken);
    }

    public Task<bool> ExistsByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => ExistsAsync(Builders<WebhookEventFingerprint>.Filter.Eq(x => x.EventId, eventId), cancellationToken);

    public Task<bool> ExistsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ExistsAsync(Builders<WebhookEventFingerprint>.Filter.Eq(x => x.CorrelationId, correlationId), cancellationToken);

    public Task<bool> ExistsByPayloadHashAsync(string payloadHash, string? customerId = null, string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var b = Builders<WebhookEventFingerprint>.Filter;
        var f = b.Eq(x => x.PayloadHash, payloadHash);
        if (!string.IsNullOrWhiteSpace(customerId)) f &= b.Eq(x => x.CustomerId, customerId);
        if (!string.IsNullOrWhiteSpace(subscriptionId)) f &= b.Eq(x => x.SubscriptionId, subscriptionId);
        return ExistsAsync(f, cancellationToken);
    }

    public Task<bool> ExistsBySignatureHashAsync(string signatureHash, DateTime? receivedAfterUtc = null, CancellationToken cancellationToken = default)
    {
        var b = Builders<WebhookEventFingerprint>.Filter;
        var f = b.Eq(x => x.SignatureHash, signatureHash);
        if (receivedAfterUtc is not null) f &= b.Gte(x => x.ReceivedAtUtc, EnsureUtc(receivedAfterUtc.Value));
        return ExistsAsync(f, cancellationToken);
    }

    public Task<IReadOnlyList<WebhookEventFingerprint>> GetRecentByCustomerAsync(string customerId, DateTime receivedAfterUtc, int limit, CancellationToken cancellationToken = default)
    {
        var b = Builders<WebhookEventFingerprint>.Filter;
        return ToListAsync(b.Eq(x => x.CustomerId, customerId) & b.Gte(x => x.ReceivedAtUtc, EnsureUtc(receivedAfterUtc)), limit, cancellationToken);
    }

    public Task<IReadOnlyList<WebhookEventFingerprint>> SearchSimilarAsync(string? customerId, string? subscriptionId, string? endpointId, string? payloadHash, string? signatureHash, DateTime? receivedAfterUtc, int limit, CancellationToken cancellationToken = default)
    {
        var b = Builders<WebhookEventFingerprint>.Filter;
        var f = b.Empty;
        if (!string.IsNullOrWhiteSpace(customerId)) f &= b.Eq(x => x.CustomerId, customerId);
        if (!string.IsNullOrWhiteSpace(subscriptionId)) f &= b.Eq(x => x.SubscriptionId, subscriptionId);
        if (!string.IsNullOrWhiteSpace(endpointId)) f &= b.Eq(x => x.EndpointId, endpointId);
        if (!string.IsNullOrWhiteSpace(payloadHash)) f &= b.Eq(x => x.PayloadHash, payloadHash);
        if (!string.IsNullOrWhiteSpace(signatureHash)) f &= b.Eq(x => x.SignatureHash, signatureHash);
        if (receivedAfterUtc is not null) f &= b.Gte(x => x.ReceivedAtUtc, EnsureUtc(receivedAfterUtc.Value));
        return ToListAsync(f, limit, cancellationToken);
    }

    private async Task<bool> ExistsAsync(FilterDefinition<WebhookEventFingerprint> filter, CancellationToken cancellationToken)
        => await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken) > 0;

    private async Task<IReadOnlyList<WebhookEventFingerprint>> ToListAsync(FilterDefinition<WebhookEventFingerprint> filter, int limit, CancellationToken cancellationToken)
    {
        using var cursor = await _collection.FindAsync(filter, new FindOptions<WebhookEventFingerprint> { Limit = Math.Max(1, limit), Sort = Builders<WebhookEventFingerprint>.Sort.Descending(x => x.ReceivedAtUtc) }, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
