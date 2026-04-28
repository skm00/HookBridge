namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class CreateApiKeyRequestDto
{
    public string Name { get; set; } = string.Empty;

    public bool EnableSignatureValidation { get; set; }

    public string? SignatureSecret { get; set; }

    public string SignatureHeaderName { get; set; } = HookBridge.Domain.Entities.ApiKey.DefaultSignatureHeaderName;
}
