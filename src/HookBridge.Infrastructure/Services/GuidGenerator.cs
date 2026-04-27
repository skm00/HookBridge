using HookBridge.Application.Interfaces;

namespace HookBridge.Infrastructure.Services;

/// <summary>
/// GUID-based implementation of <see cref="IGuidGenerator"/>.
/// </summary>
public sealed class GuidGenerator : IGuidGenerator
{
    /// <inheritdoc />
    public string NewGuid() => Guid.NewGuid().ToString();
}
