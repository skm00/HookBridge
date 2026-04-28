using System.Net;

namespace HookBridge.Api.Security;

public sealed class ClientIpResolver : IClientIpResolver
{
    public string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstValid = forwardedFor
                .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .FirstOrDefault(value => IPAddress.TryParse(value, out _));

            if (!string.IsNullOrWhiteSpace(firstValid))
            {
                return firstValid;
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return IPAddress.TryParse(remoteIp, out _) ? remoteIp! : string.Empty;
    }
}
