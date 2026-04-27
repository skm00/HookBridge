using HookBridge.Api.Security;
using HookBridge.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

internal static class TenantIsolationTestHelpers
{
    public static TenantAccessValidator CreateValidator(
        ICurrentUserContext? currentUserContext = null,
        HttpContext? httpContext = null)
    {
        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = httpContext ?? new DefaultHttpContext(),
        };

        return new TenantAccessValidator(
            currentUserContext ?? new FakeCurrentUserContext(),
            contextAccessor,
            NullLogger<TenantAccessValidator>.Instance);
    }

    internal sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public string? UserId { get; init; } = "user-1";

        public string? TenantId { get; init; } = "tenant-1";

        public string? Email { get; init; } = "admin@tenant-1.com";

        public string? Role { get; init; } = "Admin";

        public bool IsAuthenticated { get; init; } = true;
    }
}
