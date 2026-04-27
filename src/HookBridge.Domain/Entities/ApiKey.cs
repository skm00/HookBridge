namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents an API key used for tenant-scoped ingestion authentication.
/// </summary>
public sealed class ApiKey : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
