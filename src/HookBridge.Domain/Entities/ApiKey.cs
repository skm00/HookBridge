namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents an API key used for tenant-scoped ingestion authentication.
/// </summary>
public sealed class ApiKey : BaseEntity
{
    public const string DefaultSignatureHeaderName = "x-hookbridge-signature";

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool EnableSignatureValidation { get; set; }

    public string? SignatureSecret { get; set; }

    public string SignatureHeaderName { get; set; } = DefaultSignatureHeaderName;

    public List<string>? AllowedIpAddresses { get; set; }
}
