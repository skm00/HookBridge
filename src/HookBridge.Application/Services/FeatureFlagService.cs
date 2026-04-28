using HookBridge.Application.Configuration;
using HookBridge.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Services;

public sealed class FeatureFlagService(IOptionsMonitor<FeatureFlagsSettings> settings) : IFeatureFlagService
{
    public bool IsEnabled(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        return TryGetGlobalFlag(flagName, out var isEnabled) && isEnabled;
    }

    public bool IsEnabled(string flagName, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tenantId) && TryGetTenantOverride(flagName, tenantId, out var overrideEnabled))
        {
            return overrideEnabled;
        }

        return IsEnabled(flagName);
    }

    private bool TryGetGlobalFlag(string flagName, out bool isEnabled)
    {
        var flags = settings.CurrentValue.Flags;
        if (flags.TryGetValue(flagName, out isEnabled))
        {
            return true;
        }

        var caseInsensitiveLookup = flags
            .FirstOrDefault(x => string.Equals(x.Key, flagName, StringComparison.OrdinalIgnoreCase));

        if (caseInsensitiveLookup.Key is not null)
        {
            isEnabled = caseInsensitiveLookup.Value;
            return true;
        }

        isEnabled = false;
        return false;
    }

    private bool TryGetTenantOverride(string flagName, string tenantId, out bool isEnabled)
    {
        var overrideMatch = settings.CurrentValue.TenantFeatureOverrides
            .FirstOrDefault(x =>
                string.Equals(x.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.FlagName, flagName, StringComparison.OrdinalIgnoreCase));

        if (overrideMatch is not null)
        {
            isEnabled = overrideMatch.IsEnabled;
            return true;
        }

        isEnabled = false;
        return false;
    }
}
