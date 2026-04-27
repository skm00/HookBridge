using HookBridge.Application.Interfaces;
using HookBridge.Worker;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class WorkerTransactionRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesException_AndRethrows()
    {
        var transaction = new RecordingTraceTransaction();
        var tracing = new RecordingTracingService(transaction);
        var runner = new WorkerTransactionRunner(tracing);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(
                "Process webhook event",
                t => t.SetLabel("eventId", "evt-1"),
                _ => Task.FromException(new InvalidOperationException("boom")),
                CancellationToken.None));

        Assert.True(transaction.ExceptionCaptured);
        Assert.True(transaction.EndCalled);
    }

    private sealed class RecordingTracingService(RecordingTraceTransaction transaction) : ITracingService
    {
        public ITraceTransaction StartTransaction(string name, string type) => transaction;
        public Task<T> CaptureSpanAsync<T>(string name, string type, Func<Task<T>> action) => action();
        public Task CaptureSpanAsync(string name, string type, Func<Task> action) => action();
    }

    private sealed class RecordingTraceTransaction : ITraceTransaction
    {
        public bool ExceptionCaptured { get; private set; }
        public bool EndCalled { get; private set; }

        public void SetLabel(string key, string? value) { }
        public void SetLabel(string key, int value) { }
        public void CaptureException(Exception exception) => ExceptionCaptured = true;
        public void End() => EndCalled = true;
        public void Dispose() { }
    }
}
