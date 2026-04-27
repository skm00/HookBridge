using System.Security.Cryptography;
using HookBridge.Application.Interfaces.Security;

namespace HookBridge.Infrastructure.Services;

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        var random = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"hb_live_{random}";
    }

    public string GetKeyPrefix(string plainApiKey)
    {
        const string Prefix = "hb_live_";
        var suffix = plainApiKey.StartsWith(Prefix, StringComparison.Ordinal)
            ? plainApiKey[Prefix.Length..]
            : plainApiKey;

        var visible = suffix.Length >= 4 ? suffix[..4] : suffix;
        return $"{Prefix}{visible}****";
    }
}
