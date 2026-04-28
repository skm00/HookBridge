namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class UpdateApiKeyRequestDto
{
    public List<string>? AllowedIpAddresses { get; set; }
}
