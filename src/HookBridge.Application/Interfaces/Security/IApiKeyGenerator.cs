namespace HookBridge.Application.Interfaces.Security;

public interface IApiKeyGenerator
{
    string Generate();

    string GetKeyPrefix(string plainApiKey);
}
