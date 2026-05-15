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

    [Fact]
    public void XForwardedFor_InvalidValuesFallBackToRemoteIp()
    {
        var resolver = new ClientIpResolver();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "unknown, also-invalid";
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");

        var ip = resolver.GetClientIp(context);

        Assert.Equal("198.51.100.10", ip);
    }

    [Fact]
    public void MissingForwardedFor_ReturnsRemoteIp()
    {
        var resolver = new ClientIpResolver();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8::1");

        var ip = resolver.GetClientIp(context);

        Assert.Equal("2001:db8::1", ip);
    }

    [Fact]
    public void InvalidRemoteIp_ReturnsEmptyString()
    {
        var resolver = new ClientIpResolver();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;

        var ip = resolver.GetClientIp(context);

        Assert.Equal(string.Empty, ip);
    }

}
