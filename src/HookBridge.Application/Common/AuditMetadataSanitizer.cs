using System.Collections;

namespace HookBridge.Application.Common;

public static class AuditMetadataSanitizer
{
    private static readonly string[] SensitiveKeys =
    [
        "password",
        "secret",
        "authorization",
        "token",
        "apikey",
        "api_key",
        "hmac",
        "oauth",
        "jwt",
        "stripe",
    ];

    public static object? Sanitize(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        if (metadata is IDictionary<string, object?> dict)
        {
            return SanitizeDictionary(dict);
        }

        if (metadata is IDictionary nonGenericDictionary)
        {
            var converted = new Dictionary<string, object?>();
            foreach (DictionaryEntry entry in nonGenericDictionary)
            {
                converted[entry.Key?.ToString() ?? string.Empty] = entry.Value;
            }

            return SanitizeDictionary(converted);
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, object?> SanitizeDictionary(IDictionary<string, object?> dictionary)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in dictionary)
        {
            if (IsSensitiveKey(pair.Key))
            {
                sanitized[pair.Key] = "[REDACTED]";
                continue;
            }

            sanitized[pair.Key] = pair.Value switch
            {
                IDictionary<string, object?> nested => SanitizeDictionary(nested),
                IDictionary nestedNonGeneric => Sanitize(nestedNonGeneric),
                _ => pair.Value,
            };
        }

        return sanitized;
    }

    private static bool IsSensitiveKey(string key)
        => SensitiveKeys.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
