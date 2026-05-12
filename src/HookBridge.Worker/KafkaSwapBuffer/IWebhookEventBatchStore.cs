namespace HookBridge.Worker.KafkaSwapBuffer;

public sealed record WebhookEventBatchPersistenceResult(int InsertedCount, int DuplicateCount, int FailedInsertCount);

public interface IWebhookEventBatchStore
{
    Task EnsureUniqueEventIdIndexAsync(CancellationToken cancellationToken);

    Task<WebhookEventBatchPersistenceResult> InsertAsync(
        IReadOnlyCollection<BufferedWebhookEvent> events,
        CancellationToken cancellationToken);
}
