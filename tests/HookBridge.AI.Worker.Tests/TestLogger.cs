using Microsoft.Extensions.Logging;

namespace HookBridge.AI.Worker.Tests;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly object _syncRoot = new();
    private readonly List<LogRecord> _records = new();

    public IReadOnlyList<LogRecord> Records
    {
        get
        {
            lock (_syncRoot)
            {
                return _records.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_syncRoot)
        {
            _records.Add(new LogRecord(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);
