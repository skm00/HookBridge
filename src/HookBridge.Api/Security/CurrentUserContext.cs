using System.Security.Claims;
using HookBridge.Application.Interfaces;

namespace HookBridge.Api.Security;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public string? UserId => GetClaimValue(ClaimTypes.NameIdentifier) ?? GetClaimValue("sub");

    public string? TenantId => GetClaimValue("tenantId");

    public string? Email => GetClaimValue(ClaimTypes.Email) ?? GetClaimValue("email");

    public string? Role => GetClaimValue(ClaimTypes.Role) ?? GetClaimValue("role");

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    private string? GetClaimValue(string claimType)
        => httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
}
