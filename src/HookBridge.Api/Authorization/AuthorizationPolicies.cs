namespace HookBridge.Api.Authorization;

public static class AuthorizationPolicies
{
    public const string OwnerOnly = nameof(OwnerOnly);
    public const string AdminOrOwner = nameof(AdminOrOwner);
    public const string DeveloperOrAbove = nameof(DeveloperOrAbove);
    public const string ViewerOrAbove = nameof(ViewerOrAbove);
}
