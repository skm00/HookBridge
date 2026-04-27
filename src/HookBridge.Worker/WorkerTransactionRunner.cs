using HookBridge.Application.Interfaces;

namespace HookBridge.Worker;

public sealed class WorkerTransactionRunner(ITracingService tracingService)
{
    public async Task RunAsync(
        string transactionName,
        Action<ITraceTransaction> setLabels,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var transaction = tracingService.StartTransaction(transactionName, "worker");
        setLabels(transaction);

        try
        {
            await action(cancellationToken);
        }
        catch (Exception ex)
        {
            transaction.CaptureException(ex);
            throw;
        }
        finally
        {
            transaction.End();
        }
    }
}
