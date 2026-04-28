using System.Net;
using HookBridge.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class ClientIpResolverTests
{
    [Fact]
    public void XForwardedFor_FirstValidIpIsReturned()
    {
        var resolver = new ClientIpResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "unknown, 203.0.113.5, 198.51.100.1";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        var ip = resolver.GetClientIp(context);

        Assert.Equal("203.0.113.5", ip);
    }
}
