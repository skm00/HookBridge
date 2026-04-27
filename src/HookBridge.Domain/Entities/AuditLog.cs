namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents an immutable admin audit trail entry.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string? UserEmail { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public string? Description { get; set; }

    public object? Metadata { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

}
