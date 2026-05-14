namespace HookBridge.AI.Worker.Logging;

public static class SensitiveLogSanitizer
{
    private const string MaskedValue = "[MASKED]";

    private static readonly string[] SensitiveFragments =
    [
        "authorization",
        "cookie",
        "set-cookie",
        "api-key",
        "apikey",
        "token",
        "secret",
        "password",
        "connectionstring",
        "connection-string"
    ];

    public static string? MaskIfSensitive(string? name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return IsSensitiveName(name) ? MaskedValue : value;
    }

    public static IReadOnlyDictionary<string, string?> MaskSensitiveValues(IReadOnlyDictionary<string, string?> values)
        => values.ToDictionary(pair => pair.Key, pair => MaskIfSensitive(pair.Key, pair.Value), StringComparer.OrdinalIgnoreCase);

    public static bool IsSensitiveName(string? name)
        => !string.IsNullOrWhiteSpace(name) &&
           SensitiveFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
