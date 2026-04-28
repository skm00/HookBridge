namespace HookBridge.Application.Configuration;

public sealed class FeatureFlagsSettings
{
    public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<TenantFeatureOverride> TenantFeatureOverrides { get; set; } = [];
}

public sealed class TenantFeatureOverride
{
    public string TenantId { get; set; } = string.Empty;

    public string FlagName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
