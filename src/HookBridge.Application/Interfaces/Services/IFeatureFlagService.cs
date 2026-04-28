namespace HookBridge.Application.Interfaces.Services;

public interface IFeatureFlagService
{
    bool IsEnabled(string flagName);

    bool IsEnabled(string flagName, string tenantId);
}
