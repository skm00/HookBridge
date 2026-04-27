using Elastic.Apm;
using Elastic.Apm.Api;
using HookBridge.Application.Interfaces;

namespace HookBridge.Infrastructure.Services;

public sealed class ElasticApmTracingService : ITracingService
{
    public ITraceTransaction StartTransaction(string name, string type)
    {
        var transaction = Agent.Tracer.StartTransaction(name, type);
        return new ElasticTraceTransaction(transaction);
    }

    public async Task<T> CaptureSpanAsync<T>(string name, string type, Func<Task<T>> action)
    {
        var currentTransaction = Agent.Tracer.CurrentTransaction;
        if (currentTransaction is null)
        {
            return await action();
        }

        return await currentTransaction.CaptureSpan(name, type, action);
    }

    public async Task CaptureSpanAsync(string name, string type, Func<Task> action)
    {
        var currentTransaction = Agent.Tracer.CurrentTransaction;
        if (currentTransaction is null)
        {
            await action();
            return;
        }

        await currentTransaction.CaptureSpan(name, type, action);
    }

    private sealed class ElasticTraceTransaction(ITransaction? transaction) : ITraceTransaction
    {
        public void SetLabel(string key, string? value)
        {
            transaction?.SetLabel(key, value ?? string.Empty);
        }

        public void SetLabel(string key, int value)
        {
            transaction?.SetLabel(key, value);
        }

        public void CaptureException(Exception exception)
        {
            transaction?.CaptureException(exception);
        }

        public void End()
        {
            transaction?.End();
        }

        public void Dispose()
        {
            End();
        }
    }
}
