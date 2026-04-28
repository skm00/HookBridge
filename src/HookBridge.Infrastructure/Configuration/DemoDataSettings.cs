namespace HookBridge.Infrastructure.Configuration;

public sealed class DemoDataSettings
{
    public bool Enabled { get; set; }

    public string AdminEmail { get; set; } = "demo@hookbridge.local";

    public string AdminPassword { get; set; } = "DemoPassword123!";

    public string TenantName { get; set; } = "Demo Company";

    public string TenantSlug { get; set; } = "demo-company";
}
