namespace HookBridge.Application.Interfaces;

/// <summary>
/// Abstraction for creating unique identifiers.
/// </summary>
public interface IGuidGenerator
{
    /// <summary>
    /// Creates a new GUID string.
    /// </summary>
    /// <returns>A newly generated GUID represented as a string.</returns>
    string NewGuid();
}
