using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiProcessingWorkerTests
{
    [Fact]
    public async Task StartAsync_LogsStartupMessage()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()));

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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()));

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
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()));

        using var cancellation = new CancellationTokenSource();
        await worker.StartAsync(cancellation.Token);

        logger.Records.Should().NotContain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));

        await worker.StopAsync(CancellationToken.None);

        logger.Records.Should().Contain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));
    }
}
