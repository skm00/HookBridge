namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class ApiKeyValidationResult
{
    public bool IsValid { get; set; }

    public string? TenantId { get; set; }

    public string? ApiKeyId { get; set; }

    public string? FailureReason { get; set; }
}
