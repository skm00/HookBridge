namespace HookBridge.Application.Interfaces;

/// <summary>
/// Abstraction for retrieving current UTC date and time.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }
}
