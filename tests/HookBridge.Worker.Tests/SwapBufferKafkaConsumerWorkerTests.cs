using Confluent.Kafka;
using HookBridge.Worker.KafkaSwapBuffer;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class SwapBufferKafkaConsumerWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_FlushesOnBatchSize()
    {
        var consumer = new FakeConsumer(Messages("evt-1", "evt-2"));
        var store = new FakeStore();
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 2);

        await RunUntilIdleAsync(worker, consumer);

        Assert.Contains(store.Batches, batch => batch.Count == 2);
    }

    [Fact]
    public async Task ExecuteAsync_FlushesOnInterval()
    {
        var consumer = new FakeConsumer(Messages("evt-1"));
        var store = new FakeStore();
        var worker = CreateWorker(consumer, store, options =>
        {
            options.BatchSize = 500;
            options.FlushIntervalSeconds = 0;
        });

        await RunUntilIdleAsync(worker, consumer);

        Assert.Single(store.Batches);
        Assert.Single(store.Batches[0]);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCommitBeforeMongoInsertCompletes()
    {
        var consumer = new FakeConsumer(Messages("evt-1"));
        var insertStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInsert = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FakeStore
        {
            OnInsertAsync = async _ =>
            {
                insertStarted.SetResult();
                await releaseInsert.Task;
                return new WebhookEventBatchPersistenceResult(1, 0, 0);
            },
        };
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 1);
        using var cts = new CancellationTokenSource();
        consumer.CancelWhenIdle = () => cts.Cancel();

        var runTask = worker.RunAsync(cts.Token);
        try
        {
            await insertStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Empty(consumer.CommittedOffsets);

            releaseInsert.SetResult();
            await WaitUntilAsync(() => consumer.CommittedOffsets.Count == 1);
        }
        finally
        {
            releaseInsert.TrySetResult();
            await cts.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ExecuteAsync_CommitsAfterSuccessfulInsert()
    {
        var consumer = new FakeConsumer(Messages("evt-1", "evt-2"));
        var store = new FakeStore();
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 2);

        await RunUntilIdleAsync(worker, consumer);

        var committed = Assert.Single(consumer.CommittedOffsets);
        Assert.Equal("webhook-events", committed.Topic);
        Assert.Equal(0, committed.Partition.Value);
        Assert.Equal(2, committed.Offset.Value);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateEventIdDoesNotBreakService()
    {
        var consumer = new FakeConsumer(Messages("evt-1", "evt-1"));
        var store = new FakeStore
        {
            OnInsertAsync = _ => Task.FromResult(new WebhookEventBatchPersistenceResult(1, 1, 0)),
        };
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 2);

        await RunUntilIdleAsync(worker, consumer);

        Assert.Single(store.Batches);
        Assert.Single(consumer.CommittedOffsets);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceContinuesAfterMongoException()
    {
        var consumer = new FakeConsumer(Messages("evt-1", "evt-2"));
        var callCount = 0;
        var store = new FakeStore
        {
            OnInsertAsync = _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new MongoException("temporary outage");
                }

                return Task.FromResult(new WebhookEventBatchPersistenceResult(2, 0, 0));
            },
        };
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 1);

        await RunUntilIdleAsync(worker, consumer);

        Assert.True(callCount >= 2);
        Assert.NotEmpty(consumer.CommittedOffsets);
    }

    [Fact]
    public async Task ExecuteAsync_ShutdownFlushesRemainingRecords()
    {
        var consumer = new FakeConsumer(Messages("evt-1"));
        var store = new FakeStore();
        var worker = CreateWorker(consumer, store, options => options.BatchSize = 500);

        await RunUntilIdleAsync(worker, consumer);

        Assert.Single(store.Batches);
        Assert.Single(store.Batches[0]);
        Assert.Single(consumer.CommittedOffsets);
    }

    private static TestSwapBufferKafkaConsumerWorker CreateWorker(
        FakeConsumer consumer,
        FakeStore store,
        Action<KafkaConsumerOptions>? configure = null)
    {
        var options = new KafkaConsumerOptions
        {
            BootstrapServers = "localhost:9092",
            GroupId = "hookbridge-worker",
            TopicName = "webhook-events",
            MongoCollectionName = "webhook_events",
            BatchSize = 500,
            FlushIntervalSeconds = 5,
            MaxBufferSize = 10_000,
            EnableBackpressure = true,
        };
        configure?.Invoke(options);

        return new TestSwapBufferKafkaConsumerWorker(
            new FakeConsumerFactory(consumer),
            store,
            Options.Create(options),
            new TestLogger<SwapBufferKafkaConsumerWorker>());
    }

    private static async Task RunUntilIdleAsync(TestSwapBufferKafkaConsumerWorker worker, FakeConsumer consumer)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        consumer.CancelWhenIdle = () => cts.Cancel();
        await worker.RunAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static IReadOnlyList<ConsumeResult<string, WebhookEvent>> Messages(params string[] eventIds)
    {
        return eventIds.Select((eventId, index) => new ConsumeResult<string, WebhookEvent>
        {
            TopicPartitionOffset = new TopicPartitionOffset("webhook-events", new Partition(0), new Offset(index)),
            Message = new Message<string, WebhookEvent>
            {
                Key = eventId,
                Value = new WebhookEvent
                {
                    EventId = eventId,
                    TenantId = "tenant-1",
                    EventType = "webhook.received",
                    ReceivedAt = DateTime.UtcNow,
                },
            },
        }).ToList();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private sealed class TestSwapBufferKafkaConsumerWorker : SwapBufferKafkaConsumerWorker
    {
        public TestSwapBufferKafkaConsumerWorker(
            IKafkaSwapBufferConsumerFactory consumerFactory,
            IWebhookEventBatchStore eventStore,
            IOptions<KafkaConsumerOptions> options,
            TestLogger<SwapBufferKafkaConsumerWorker> logger)
            : base(consumerFactory, eventStore, options, logger)
        {
        }

        public Task RunAsync(CancellationToken cancellationToken) => ExecuteAsync(cancellationToken);
    }

    private sealed class FakeConsumerFactory : IKafkaSwapBufferConsumerFactory
    {
        private readonly IKafkaSwapBufferConsumer _consumer;

        public FakeConsumerFactory(IKafkaSwapBufferConsumer consumer)
        {
            _consumer = consumer;
        }

        public IKafkaSwapBufferConsumer Create(KafkaConsumerOptions options) => _consumer;
    }

    private sealed class FakeConsumer : IKafkaSwapBufferConsumer
    {
        private readonly Queue<ConsumeResult<string, WebhookEvent>> _messages;

        public FakeConsumer(IEnumerable<ConsumeResult<string, WebhookEvent>> messages)
        {
            _messages = new Queue<ConsumeResult<string, WebhookEvent>>(messages);
        }

        public IReadOnlyCollection<TopicPartition> Assignment { get; } = new[] { new TopicPartition("webhook-events", new Partition(0)) };

        public List<TopicPartitionOffset> CommittedOffsets { get; } = [];

        public Action? CancelWhenIdle { get; set; }

        public void Subscribe(string topic) { }

        public ConsumeResult<string, WebhookEvent>? Consume(TimeSpan timeout)
        {
            if (_messages.TryDequeue(out var message))
            {
                return message;
            }

            CancelWhenIdle?.Invoke();
            return null;
        }

        public void Commit(IEnumerable<TopicPartitionOffset> offsets) => CommittedOffsets.AddRange(offsets);

        public void Pause(IEnumerable<TopicPartition> partitions) { }

        public void Resume(IEnumerable<TopicPartition> partitions) { }

        public void Dispose() { }
    }

    private sealed class FakeStore : IWebhookEventBatchStore
    {
        public List<IReadOnlyList<BufferedWebhookEvent>> Batches { get; } = [];

        public Func<IReadOnlyCollection<BufferedWebhookEvent>, Task<WebhookEventBatchPersistenceResult>>? OnInsertAsync { get; init; }

        public Task EnsureUniqueEventIdIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<WebhookEventBatchPersistenceResult> InsertAsync(
            IReadOnlyCollection<BufferedWebhookEvent> events,
            CancellationToken cancellationToken)
        {
            Batches.Add(events.ToList());
            if (OnInsertAsync is not null)
            {
                return await OnInsertAsync(events);
            }

            return new WebhookEventBatchPersistenceResult(events.Count, 0, 0);
        }
    }
}
