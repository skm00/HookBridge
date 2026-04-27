namespace HookBridge.Application.Exceptions;

/// <summary>
/// Represents a conflict in a requested operation.
/// </summary>
public sealed class ConflictException(string message) : Exception(message)
{
}
