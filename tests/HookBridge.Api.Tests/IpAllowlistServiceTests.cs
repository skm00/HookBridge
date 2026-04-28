using HookBridge.Api.Security;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class IpAllowlistServiceTests
{
    private readonly IpAllowlistService _service = new();

    [Fact]
    public void ExactIpMatch_IsAllowed()
    {
        var isAllowed = _service.IsAllowed("192.168.1.10", ["192.168.1.10"]);

        Assert.True(isAllowed);
    }

    [Fact]
    public void CidrRangeMatch_IsAllowed()
    {
        var isAllowed = _service.IsAllowed("10.0.0.42", ["10.0.0.0/24"]);

        Assert.True(isAllowed);
    }

    [Fact]
    public void IpOutsideRange_IsBlocked()
    {
        var isAllowed = _service.IsAllowed("10.0.1.10", ["10.0.0.0/24"]);

        Assert.False(isAllowed);
    }

    [Fact]
    public void EmptyAllowlist_AllowsAll()
    {
        var isAllowed = _service.IsAllowed("1.2.3.4", []);

        Assert.True(isAllowed);
    }

    [Fact]
    public void InvalidIpInput_IsHandledSafely()
    {
        var isAllowed = _service.IsAllowed("not-an-ip", ["10.0.0.0/24", "invalid-entry"]);

        Assert.False(isAllowed);
    }
}
