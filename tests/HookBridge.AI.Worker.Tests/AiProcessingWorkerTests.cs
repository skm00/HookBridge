using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiProcessingWorkerTests
{
    [Fact]
    public async Task StartAsync_LogsStartupMessage()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("HookBridge AI Worker starting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopAsync_AfterStart_LogsShutdownMessage()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");
        await worker.StopAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker shutting down");

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("HookBridge AI Worker shutting down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_RunsUntilCancellation()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository());

        using var cancellation = new CancellationTokenSource();
        await worker.StartAsync(cancellation.Token);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");

        logger.Records.Should().NotContain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));

        await worker.StopAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker shutting down");

        logger.Records.Should().Contain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_WhenAiDisabled_DoesNotInitializeSemanticKernel()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Enabled = false }),
            kernelFactory,
            new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "AI is disabled");
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(0);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("AI is disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_WhenAiEnabled_VerifiesSemanticKernelCreation()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository());

        await worker.StartAsync(CancellationToken.None);
        await kernelFactory.WaitForCreateKernelAsync();
        await WaitForLogAsync(logger, "Semantic Kernel startup verification completed");
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(1);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("Semantic Kernel startup verification completed", StringComparison.Ordinal));
    }


    [Fact]
    public async Task StartAsync_WhenEventConsumed_StoresPlaceholderAnalysisResult()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var repository = new TestAiAnalysisResultRepository();
        var analysisEvent = new AiAnalysisEventDto
        {
            EventId = "evt-123",
            CorrelationId = "corr-123",
            Source = "unit-test",
            EventType = "webhook.delivery.failed",
            FailureReason = "HTTP 500",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var consumer = new TestAiAnalysisConsumer(analysisEvent);
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Provider = "Ollama", Model = "llama3.1" }),
            kernelFactory,
            consumer,
            repository);

        await worker.StartAsync(CancellationToken.None);
        await repository.WaitForInsertAsync();
        await worker.StopAsync(CancellationToken.None);

        repository.InsertedResults.Should().ContainSingle();
        var result = repository.InsertedResults.Single();
        result.EventId.Should().Be("evt-123");
        result.CorrelationId.Should().Be("corr-123");
        result.Source.Should().Be("unit-test");
        result.EventType.Should().Be("webhook.delivery.failed");
        result.FailureReason.Should().Be("HTTP 500");
        result.AiSummary.Should().Contain("HTTP 500");
        result.AiRecommendation.Should().NotBeNullOrWhiteSpace();
        result.RiskLevel.Should().Be("Unknown");
        result.ConfidenceScore.Should().Be(0);
        result.Provider.Should().Be("Ollama");
        result.Model.Should().Be("llama3.1");
        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static async Task WaitForLogAsync<T>(TestLogger<T> logger, string message)
    {
        await WaitForConditionAsync(() => logger.Records.Any(record =>
            record.Message.Contains(message, StringComparison.Ordinal)));
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!predicate())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class TestAiAnalysisConsumer : IAiAnalysisConsumer
    {
        private readonly AiAnalysisEventDto? _analysisEvent;

        public TestAiAnalysisConsumer(AiAnalysisEventDto? analysisEvent = null)
        {
            _analysisEvent = analysisEvent;
        }

        public async IAsyncEnumerable<AiAnalysisEventDto> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_analysisEvent is not null)
            {
                yield return _analysisEvent;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class TestAiAnalysisResultRepository : IAiAnalysisResultRepository
    {
        private readonly TaskCompletionSource _insertCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AiAnalysisResult> InsertedResults { get; } = new();

        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default)
        {
            InsertedResults.Add(result);
            _insertCalled.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);

        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);

        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(Array.Empty<AiAnalysisResult>());

        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(Array.Empty<AiAnalysisResult>());

        public Task WaitForInsertAsync() => _insertCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class TestKernelFactory : IKernelFactory
    {
        private readonly TaskCompletionSource _createKernelCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _createKernelCallCount;

        public int CreateKernelCallCount => Volatile.Read(ref _createKernelCallCount);

        public Kernel CreateKernel()
        {
            Interlocked.Increment(ref _createKernelCallCount);
            _createKernelCalled.TrySetResult();
            return Kernel.CreateBuilder().Build();
        }

        public Task WaitForCreateKernelAsync() => _createKernelCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
