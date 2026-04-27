using HookBridge.Application.Interfaces;

namespace HookBridge.Infrastructure.Services;

/// <summary>
/// System clock implementation of <see cref="IDateTimeProvider"/>.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
