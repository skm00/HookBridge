namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class ApiKeyResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool EnableSignatureValidation { get; set; }

    public string SignatureHeaderName { get; set; } = string.Empty;

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<string>? AllowedIpAddresses { get; set; }
}
