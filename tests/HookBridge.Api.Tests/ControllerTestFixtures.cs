using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Tests;

internal sealed class ControllerTestFixture
{
    public DefaultHttpContext HttpContext { get; } = new();

    public T AttachHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = HttpContext };
        return controller;
    }
}

internal static class TestMockFactory
{
    public static TenantIsolationTestHelpers.FakeCurrentUserContext AuthenticatedUser(string tenantId = "tenant-1") => new()
    {
        TenantId = tenantId,
        UserId = "user-1",
        Email = $"admin@{tenantId}.example",
        Role = "Admin",
        IsAuthenticated = true,
    };
}
