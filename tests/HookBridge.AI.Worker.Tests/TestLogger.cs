using Microsoft.Extensions.Logging;

namespace HookBridge.AI.Worker.Tests;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly object _syncRoot = new();
    private readonly List<LogRecord> _records = new();
    private readonly List<IReadOnlyDictionary<string, object?>> _scopes = new();

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

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes
    {
        get
        {
            lock (_syncRoot)
            {
                return _scopes.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var properties = ExtractProperties(state);
        lock (_syncRoot)
        {
            _scopes.Add(properties);
        }

        return NullScope.Instance;
    }

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
            _records.Add(new LogRecord(logLevel, formatter(state, exception), exception, ExtractProperties(state)));
        }
    }

    private static IReadOnlyDictionary<string, object?> ExtractProperties<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            return properties
                .Where(property => property.Key != "{OriginalFormat}")
                .ToDictionary(property => property.Key, property => property.Value, StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>();
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record LogRecord(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);
