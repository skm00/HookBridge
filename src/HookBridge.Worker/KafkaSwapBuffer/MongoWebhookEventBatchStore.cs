using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.Worker.KafkaSwapBuffer;

public sealed class MongoWebhookEventBatchStore : IWebhookEventBatchStore
{
    private readonly IMongoCollection<BufferedWebhookEvent> _collection;

    public MongoWebhookEventBatchStore(IMongoDatabase database, IOptions<KafkaConsumerOptions> options)
    {
        _collection = database.GetCollection<BufferedWebhookEvent>(options.Value.MongoCollectionName);
    }

    /// <summary>
    /// Creates a unique EventId index so at-least-once Kafka replay cannot duplicate persisted webhook records.
    /// </summary>
    public Task EnsureUniqueEventIdIndexAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<BufferedWebhookEvent>.IndexKeys.Ascending(x => x.EventId);
        var model = new CreateIndexModel<BufferedWebhookEvent>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_webhook_events_event_id" });

        return _collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Uses unordered bulk insert so one duplicate EventId does not prevent MongoDB from persisting the rest of the batch.
    /// Duplicate key errors are safe because the unique EventId index proves the replayed event is already durable.
    /// </summary>
    public async Task<WebhookEventBatchPersistenceResult> InsertAsync(
        IReadOnlyCollection<BufferedWebhookEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return new WebhookEventBatchPersistenceResult(0, 0, 0);
        }

        try
        {
            await _collection.InsertManyAsync(
                events,
                new InsertManyOptions { IsOrdered = false },
                cancellationToken);

            return new WebhookEventBatchPersistenceResult(events.Count, 0, 0);
        }
        catch (MongoBulkWriteException<BufferedWebhookEvent> ex) when (ex.WriteErrors.All(IsDuplicateKeyError))
        {
            var duplicateCount = ex.WriteErrors.Count;
            return new WebhookEventBatchPersistenceResult(events.Count - duplicateCount, duplicateCount, 0);
        }
        catch (MongoBulkWriteException<BufferedWebhookEvent> ex)
        {
            var duplicateCount = ex.WriteErrors.Count(IsDuplicateKeyError);
            var failedCount = ex.WriteErrors.Count - duplicateCount;
            throw new WebhookEventBatchPersistenceException(duplicateCount, failedCount, ex);
        }
    }

    private static bool IsDuplicateKeyError(BulkWriteError error) => error.Code is 11000 or 11001 or 12582;
}

public sealed class WebhookEventBatchPersistenceException : Exception
{
    public WebhookEventBatchPersistenceException(int duplicateCount, int failedInsertCount, Exception innerException)
        : base("MongoDB bulk webhook event persistence failed.", innerException)
    {
        DuplicateCount = duplicateCount;
        FailedInsertCount = failedInsertCount;
    }

    public int DuplicateCount { get; }

    public int FailedInsertCount { get; }
}
