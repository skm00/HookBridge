using HookBridge.Worker;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class WorkerCurrentUserContextTests
{
    [Fact]
    public void IsAuthenticated_WhenRunningBackgroundWorker_ShouldReturnFalse()
    {
        var context = new WorkerCurrentUserContext();

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Null(context.Email);
        Assert.Null(context.Role);
    }
}
