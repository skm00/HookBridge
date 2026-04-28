using System.Net;
using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Validation.Subscriptions;

internal static class SecurityValidationHelpers
{
    private static readonly HashSet<string> RestrictedOutboundHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Content-Length",
        "Transfer-Encoding",
        "Connection",
    };

    public static bool BeValidHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPrivateOrLocalNetworkTarget(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var ipAddress))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ipAddress.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 127;
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return ipAddress.IsIPv6LinkLocal
                || ipAddress.IsIPv6SiteLocal
                || ipAddress.IsIPv6UniqueLocal
                || ipAddress.Equals(IPAddress.IPv6Loopback);
        }

        return false;
    }

    public static bool HaveUniqueHeaderNames(List<KeyValueDto>? headers)
        => headers is not null && headers
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .All(group => group.Count() == 1);

    public static bool IsSafeHeaderName(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !ContainsCrLf(value)
            && value.Length <= HookBridge.Shared.Constants.ValidationLimits.MaxHeaderNameLength;

    public static bool IsSafeHeaderValue(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && !ContainsCrLf(value)
            && value.Length <= HookBridge.Shared.Constants.ValidationLimits.MaxHeaderValueLength;

    public static bool ContainsCrLf(string value)
        => value.Contains('\r') || value.Contains('\n');

    public static bool IsRestrictedOutboundHeader(string headerName)
        => RestrictedOutboundHeaderNames.Contains(headerName);

    public static bool AuthenticationSetsAuthorizationHeader(AuthenticationDto? authentication)
    {
        if (authentication is null)
        {
            return false;
        }

        return authentication.Type switch
        {
            "Basic" => true,
            "OAuth2ClientCredentials" => true,
            "ApiKeyHeader" => authentication.ApiKeyHeader is not null
                && authentication.ApiKeyHeader.HeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
