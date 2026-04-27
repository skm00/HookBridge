using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Interfaces.Security;

namespace HookBridge.Infrastructure.Services;

public sealed class ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string plainApiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plainApiKey);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    public bool Verify(string plainApiKey, string keyHash)
    {
        var computedHash = Hash(plainApiKey);
        var computedBytes = Encoding.UTF8.GetBytes(computedHash);
        var storedBytes = Encoding.UTF8.GetBytes(keyHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
