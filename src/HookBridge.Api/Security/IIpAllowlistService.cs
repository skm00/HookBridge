namespace HookBridge.Api.Security;

public interface IIpAllowlistService
{
    bool IsAllowed(string clientIp, List<string>? allowlist);
}
