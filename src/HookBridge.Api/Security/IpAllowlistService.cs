using System.Net;
using System.Net.Sockets;

namespace HookBridge.Api.Security;

public sealed class IpAllowlistService : IIpAllowlistService
{
    public bool IsAllowed(string clientIp, List<string>? allowlist)
    {
        if (allowlist is null || allowlist.Count == 0)
        {
            return true;
        }

        if (!IPAddress.TryParse(clientIp, out var parsedClientIp))
        {
            return false;
        }

        foreach (var entry in allowlist)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var trimmed = entry.Trim();
            if (IPAddress.TryParse(trimmed, out var exactIp))
            {
                if (exactIp.Equals(parsedClientIp))
                {
                    return true;
                }

                continue;
            }

            if (IsInCidrRange(parsedClientIp, trimmed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInCidrRange(IPAddress clientIp, string cidr)
    {
        var separatorIndex = cidr.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == cidr.Length - 1)
        {
            return false;
        }

        var networkPart = cidr[..separatorIndex];
        var prefixPart = cidr[(separatorIndex + 1)..];

        if (!IPAddress.TryParse(networkPart, out var networkIp) || !int.TryParse(prefixPart, out var prefixLength))
        {
            return false;
        }

        if (networkIp.AddressFamily != clientIp.AddressFamily)
        {
            return false;
        }

        var maxPrefix = networkIp.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        var clientBytes = clientIp.GetAddressBytes();
        var networkBytes = networkIp.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < fullBytes; index++)
        {
            if (clientBytes[index] != networkBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)~(0xFF >> remainingBits);
        return (clientBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}
