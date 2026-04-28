using System.Net;

namespace HookBridge.Application.Validation.ApiKeys;

internal static class IpAllowlistValidationHelper
{
    public static bool IsValidIpOrCidr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (IPAddress.TryParse(trimmed, out _))
        {
            return true;
        }

        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex <= 0 || slashIndex == trimmed.Length - 1)
        {
            return false;
        }

        var ipPart = trimmed[..slashIndex];
        var prefixPart = trimmed[(slashIndex + 1)..];
        if (!IPAddress.TryParse(ipPart, out var networkIp) || !int.TryParse(prefixPart, out var prefixLength))
        {
            return false;
        }

        var maxPrefix = networkIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        return prefixLength >= 0 && prefixLength <= maxPrefix;
    }
}
