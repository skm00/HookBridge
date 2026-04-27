namespace HookBridge.Application.Interfaces;

public interface ITracingService
{
    ITraceTransaction StartTransaction(string name, string type);
    Task<T> CaptureSpanAsync<T>(string name, string type, Func<Task<T>> action);
    Task CaptureSpanAsync(string name, string type, Func<Task> action);
}

public interface ITraceTransaction : IDisposable
{
    void SetLabel(string key, string? value);
    void SetLabel(string key, int value);
    void CaptureException(Exception exception);
    void End();
}
