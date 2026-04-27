namespace HookBridge.Domain.Enums;

/// <summary>
/// Defines delivery execution states.
/// </summary>
public enum DeliveryStatus
{
    /// <summary>
    /// Delivery is pending.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Delivery completed successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// Delivery failed.
    /// </summary>
    Failed = 2,
}
