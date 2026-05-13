using System.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.Worker.KafkaSwapBuffer;

/// <summary>
/// High-throughput Kafka consumer that uses a double-buffer swap strategy to keep webhook ingestion lightweight.
/// The consume loop appends records to the primary buffer and swaps buffers under a short lock when a flush is due,
/// allowing MongoDB persistence to run outside the hot path instead of blocking every Kafka poll.
/// </summary>
public class SwapBufferKafkaConsumerWorker : BackgroundService
{
    private static readonly TimeSpan ConsumePollTimeout = TimeSpan.FromMilliseconds(250);

    private readonly object _bufferLock = new();
    private readonly IKafkaSwapBufferConsumerFactory _consumerFactory;
    private readonly IWebhookEventBatchStore _eventStore;
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<SwapBufferKafkaConsumerWorker> _logger;

    private List<BufferedWebhookEvent> _primaryBuffer;
    private List<BufferedWebhookEvent> _secondaryBuffer;
    private IKafkaSwapBufferConsumer? _consumer;
    private Task? _activeFlushTask;
    private DateTime _lastFlushUtc = DateTime.UtcNow;
    private bool _paused;
    private long _consumedMessageCount;

    public SwapBufferKafkaConsumerWorker(
        IKafkaSwapBufferConsumerFactory consumerFactory,
        IWebhookEventBatchStore eventStore,
        IOptions<KafkaConsumerOptions> options,
        ILogger<SwapBufferKafkaConsumerWorker> logger)
    {
        _consumerFactory = consumerFactory;
        _eventStore = eventStore;
        _options = options.Value;
        _logger = logger;
        _primaryBuffer = new List<BufferedWebhookEvent>(_options.BatchSize);
        _secondaryBuffer = new List<BufferedWebhookEvent>(_options.BatchSize);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _eventStore.EnsureUniqueEventIdIndexAsync(cancellationToken);
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "MongoDB unique EventId index initialization failed; duplicate-safe replay requires this index.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected unique EventId index initialization failure; duplicate-safe replay requires this index.");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = _consumerFactory.Create(_options);
                _consumer = consumer;
                consumer.Subscribe(_options.TopicName);

                await ConsumeLoopAsync(consumer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error. Reason: {Reason}", ex.Error.Reason);
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "MongoDB error escaped swap-buffer consumer loop; service will continue.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected swap-buffer consumer error; service will continue.");
            }
        }

        await FlushAndWaitAsync("shutdown", CancellationToken.None);
    }

    private async Task ConsumeLoopAsync(IKafkaSwapBufferConsumer consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ApplyBackpressureAsync(consumer, stoppingToken);

                var consumeResult = consumer.Consume(ConsumePollTimeout);
                if (consumeResult is not null)
                {
                    BufferConsumedEvent(consumeResult);
                }

                if (ShouldFlushBySize())
                {
                    StartFlushIfIdle("batch size", stoppingToken);
                }
                else if (ShouldFlushByInterval())
                {
                    StartFlushIfIdle("flush interval", stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error. Reason: {Reason}", ex.Error.Reason);
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "MongoDB error in swap-buffer consumer loop; service will continue.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in swap-buffer consumer loop; service will continue.");
            }
        }
    }

    private void BufferConsumedEvent(ConsumeResult<string, WebhookEvent> consumeResult)
    {
        var buffered = BufferedWebhookEvent.FromKafka(
            consumeResult.Message.Value,
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value);

        int bufferSize;
        lock (_bufferLock)
        {
            _primaryBuffer.Add(buffered);
            bufferSize = _primaryBuffer.Count;
        }

        var consumed = Interlocked.Increment(ref _consumedMessageCount);
        _logger.LogInformation(
            "Kafka webhook event consumed. ConsumedMessageCount: {ConsumedMessageCount}, BufferSize: {BufferSize}, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, EventId: {EventId}",
            consumed,
            bufferSize,
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            buffered.EventId);
    }

    private async Task ApplyBackpressureAsync(IKafkaSwapBufferConsumer consumer, CancellationToken cancellationToken)
    {
        if (!_options.EnableBackpressure || _activeFlushTask is null || _activeFlushTask.IsCompleted)
        {
            ResumeIfPaused(consumer);
            return;
        }

        if (GetPrimaryBufferSize() < _options.MaxBufferSize)
        {
            return;
        }

        var assignment = consumer.Assignment;
        if (!_paused && assignment.Count > 0)
        {
            consumer.Pause(assignment);
            _paused = true;
            _logger.LogWarning(
                "Kafka partitions paused for swap-buffer backpressure. BufferSize: {BufferSize}, MaxBufferSize: {MaxBufferSize}, PartitionCount: {PartitionCount}",
                GetPrimaryBufferSize(),
                _options.MaxBufferSize,
                assignment.Count);
        }

        try
        {
            await _activeFlushTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Active MongoDB flush failed while backpressure was applied.");
        }
        finally
        {
            ResumeIfPaused(consumer);
        }
    }

    private void ResumeIfPaused(IKafkaSwapBufferConsumer consumer)
    {
        if (!_paused)
        {
            return;
        }

        var assignment = consumer.Assignment;
        if (assignment.Count > 0)
        {
            consumer.Resume(assignment);
            _logger.LogInformation("Kafka partitions resumed after swap-buffer flush. PartitionCount: {PartitionCount}", assignment.Count);
        }

        _paused = false;
    }

    private bool ShouldFlushBySize() => GetPrimaryBufferSize() >= _options.BatchSize;

    private bool ShouldFlushByInterval()
    {
        if (DateTime.UtcNow - _lastFlushUtc < TimeSpan.FromSeconds(_options.FlushIntervalSeconds))
        {
            return false;
        }

        return GetPrimaryBufferSize() > 0;
    }

    private int GetPrimaryBufferSize()
    {
        lock (_bufferLock)
        {
            return _primaryBuffer.Count;
        }
    }

    private void StartFlushIfIdle(string reason, CancellationToken cancellationToken)
    {
        if (_activeFlushTask is { IsCompleted: false })
        {
            return;
        }

        _activeFlushTask = FlushSwappedBufferAsync(reason, cancellationToken);
    }

    private async Task FlushAndWaitAsync(string reason, CancellationToken cancellationToken)
    {
        if (_activeFlushTask is not null)
        {
            try
            {
                await _activeFlushTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Previous swap-buffer flush failed before {FlushReason} flush.", reason);
            }
        }

        await FlushSwappedBufferAsync(reason, cancellationToken);
    }

    private async Task FlushSwappedBufferAsync(string reason, CancellationToken cancellationToken)
    {
        List<BufferedWebhookEvent> batch;
        lock (_bufferLock)
        {
            if (_primaryBuffer.Count == 0)
            {
                _lastFlushUtc = DateTime.UtcNow;
                return;
            }

            (_primaryBuffer, _secondaryBuffer) = (_secondaryBuffer, _primaryBuffer);
            batch = _secondaryBuffer;
        }

        _logger.LogInformation("Swap-buffer flush started. FlushReason: {FlushReason}, BatchSize: {BatchSize}", reason, batch.Count);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _eventStore.InsertAsync(batch, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "MongoDB webhook batch insert completed. FlushReason: {FlushReason}, BatchSize: {BatchSize}, InsertDurationMs: {InsertDurationMs}, DuplicateCount: {DuplicateCount}, FailedInsertCount: {FailedInsertCount}",
                reason,
                batch.Count,
                stopwatch.ElapsedMilliseconds,
                result.DuplicateCount,
                result.FailedInsertCount);

            CommitOffsets(batch);
            batch.Clear();
            _lastFlushUtc = DateTime.UtcNow;
        }
        catch (WebhookEventBatchPersistenceException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "MongoDB webhook batch insert failed. FlushReason: {FlushReason}, BatchSize: {BatchSize}, InsertDurationMs: {InsertDurationMs}, DuplicateCount: {DuplicateCount}, FailedInsertCount: {FailedInsertCount}. Kafka offsets were not committed.",
                reason,
                batch.Count,
                stopwatch.ElapsedMilliseconds,
                ex.DuplicateCount,
                ex.FailedInsertCount);
            RestoreFailedBatch(batch);
        }
        catch (MongoBulkWriteException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "MongoDB bulk write failed. FlushReason: {FlushReason}, BatchSize: {BatchSize}, DuplicateCount: {DuplicateCount}, FailedInsertCount: {FailedInsertCount}. Kafka offsets were not committed.",
                reason,
                batch.Count,
                0,
                batch.Count);
            RestoreFailedBatch(batch);
        }
        catch (MongoException ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "MongoDB insert failed. FlushReason: {FlushReason}, BatchSize: {BatchSize}, DuplicateCount: {DuplicateCount}, FailedInsertCount: {FailedInsertCount}. Kafka offsets were not committed.",
                reason,
                batch.Count,
                0,
                batch.Count);
            RestoreFailedBatch(batch);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Unexpected MongoDB insert error. FlushReason: {FlushReason}, BatchSize: {BatchSize}, DuplicateCount: {DuplicateCount}, FailedInsertCount: {FailedInsertCount}. Kafka offsets were not committed.",
                reason,
                batch.Count,
                0,
                batch.Count);
            RestoreFailedBatch(batch);
        }
    }

    /// <summary>
    /// Commits Kafka offsets only after MongoDB persistence succeeds. This preserves at-least-once delivery: if MongoDB
    /// fails, the offsets remain uncommitted and Kafka can replay the records; if commit fails after persistence, the
    /// unique EventId index makes the replay duplicate-safe.
    /// </summary>
    private void CommitOffsets(IReadOnlyCollection<BufferedWebhookEvent> batch)
    {
        if (_consumer is null || batch.Count == 0)
        {
            return;
        }

        var offsets = batch
            .GroupBy(x => new TopicPartition(x.Topic, new Partition(x.Partition)))
            .Select(group => new TopicPartitionOffset(group.Key, new Offset(group.Max(x => x.Offset) + 1)))
            .ToList();

        try
        {
            _consumer.Commit(offsets);
            _logger.LogInformation(
                "Kafka offsets committed after MongoDB persistence. CommittedOffsets: {CommittedOffsets}",
                string.Join(", ", offsets.Select(x => $"{x.Topic}:{x.Partition.Value}:{x.Offset.Value}")));
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "Kafka offset commit failed after MongoDB persistence. Records may replay safely due to unique EventId.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Kafka offset commit failure after MongoDB persistence.");
        }
    }

    private void RestoreFailedBatch(List<BufferedWebhookEvent> batch)
    {
        lock (_bufferLock)
        {
            if (batch.Count > 0)
            {
                _primaryBuffer.InsertRange(0, batch);
            }

            batch.Clear();
        }
    }
}
