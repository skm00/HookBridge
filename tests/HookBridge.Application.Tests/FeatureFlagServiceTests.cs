using HookBridge.Application.Configuration;
using HookBridge.Application.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class FeatureFlagServiceTests
{
    [Fact]
    public void IsEnabled_WhenFlagConfiguredTrue_ReturnsTrue()
    {
        var service = CreateService(new FeatureFlagsSettings
        {
            Flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["EnableBilling"] = true,
            },
        });

        Assert.True(service.IsEnabled("EnableBilling"));
    }

    [Fact]
    public void IsEnabled_WhenFlagMissing_ReturnsFalse()
    {
        var service = CreateService(new FeatureFlagsSettings());

        Assert.False(service.IsEnabled("MissingFlag"));
    }

    [Fact]
    public void IsEnabled_IsCaseInsensitive()
    {
        var service = CreateService(new FeatureFlagsSettings
        {
            Flags = new Dictionary<string, bool>
            {
                ["EnableEmailNotifications"] = true,
            },
        });

        Assert.True(service.IsEnabled("enableemailnotifications"));
    }

    [Fact]
    public void IsEnabled_WithTenantOverride_TakesPrecedence()
    {
        var service = CreateService(new FeatureFlagsSettings
        {
            Flags = new Dictionary<string, bool>
            {
                ["EnableBilling"] = false,
            },
            TenantFeatureOverrides =
            [
                new TenantFeatureOverride
                {
                    TenantId = "tenant-1",
                    FlagName = "EnableBilling",
                    IsEnabled = true,
                },
            ],
        });

        Assert.True(service.IsEnabled("EnableBilling", "tenant-1"));
    }

    private static FeatureFlagService CreateService(FeatureFlagsSettings settings)
        => new(new TestOptionsMonitor(settings));

    private sealed class TestOptionsMonitor(FeatureFlagsSettings currentValue) : IOptionsMonitor<FeatureFlagsSettings>
    {
        public FeatureFlagsSettings CurrentValue => currentValue;

        public FeatureFlagsSettings Get(string? name) => currentValue;

        public IDisposable? OnChange(Action<FeatureFlagsSettings, string?> listener) => null;
    }
}
