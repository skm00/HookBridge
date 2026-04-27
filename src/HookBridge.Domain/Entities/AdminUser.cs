namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents an administrative dashboard user scoped to a tenant.
/// </summary>
public sealed class AdminUser : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
