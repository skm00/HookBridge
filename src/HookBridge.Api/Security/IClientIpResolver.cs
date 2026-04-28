namespace HookBridge.Api.Security;

public interface IClientIpResolver
{
    string GetClientIp(HttpContext context);
}
