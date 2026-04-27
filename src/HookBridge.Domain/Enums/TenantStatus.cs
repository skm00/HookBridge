namespace HookBridge.Domain.Enums;

/// <summary>
/// Defines lifecycle states for a tenant.
/// </summary>
public enum TenantStatus
{
    /// <summary>
    /// The tenant is active.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The tenant is disabled.
    /// </summary>
    Disabled = 1,
}
