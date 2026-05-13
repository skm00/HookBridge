using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory);

        await worker.StartAsync(CancellationToken.None);
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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("HookBridge AI Worker shutting down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_RunsUntilCancellation()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory);

        using var cancellation = new CancellationTokenSource();
        await worker.StartAsync(cancellation.Token);

        logger.Records.Should().NotContain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));

        await worker.StopAsync(CancellationToken.None);

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
            kernelFactory);

        await worker.StartAsync(CancellationToken.None);
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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(1);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("Semantic Kernel startup verification completed", StringComparison.Ordinal));
    }

    private sealed class TestKernelFactory : IKernelFactory
    {
        public int CreateKernelCallCount { get; private set; }

        public Kernel CreateKernel()
        {
            CreateKernelCallCount++;
            return Kernel.CreateBuilder().Build();
        }
    }
}
