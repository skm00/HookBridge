namespace HookBridge.Application.DTOs.ApiKeys;

public sealed class CreateApiKeyResponseDto
{
    public string PlainApiKey { get; set; } = string.Empty;

    public ApiKeyResponseDto ApiKey { get; set; } = new();
}
