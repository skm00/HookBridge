namespace HookBridge.Api.RateLimiting;

public static class RateLimitingPolicyNames
{
    public const string EventIngestionPolicy = nameof(EventIngestionPolicy);
    public const string AdminApiPolicy = nameof(AdminApiPolicy);
    public const string PublicInboxCreationPolicy = nameof(PublicInboxCreationPolicy);
}
