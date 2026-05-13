using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer());

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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer());

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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer());

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
            new TestAiAnalysisConsumer());

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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer());

        await worker.StartAsync(CancellationToken.None);
        await kernelFactory.WaitForCreateKernelAsync();
        await WaitForLogAsync(logger, "Semantic Kernel startup verification completed");
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(1);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("Semantic Kernel startup verification completed", StringComparison.Ordinal));
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
        public async IAsyncEnumerable<AiAnalysisEventDto> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
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
