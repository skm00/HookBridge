namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class ApiKeyValidationResult
{
    public bool IsValid { get; set; }

    public string? TenantId { get; set; }

    public string? ApiKeyId { get; set; }

    public string? FailureReason { get; set; }

    public bool EnableSignatureValidation { get; set; }

    public string? SignatureSecret { get; set; }

    public string SignatureHeaderName { get; set; } = HookBridge.Domain.Entities.ApiKey.DefaultSignatureHeaderName;

    public List<string>? AllowedIpAddresses { get; set; }
}
