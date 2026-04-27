namespace HookBridge.Application.Interfaces.Security;

public interface IApiKeyHasher
{
    string Hash(string plainApiKey);

    bool Verify(string plainApiKey, string keyHash);
}
