using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.Interfaces;

public interface IOAuthTokenService
{
    Task<string> GetAccessTokenAsync(
        OAuth2ClientCredentialsDto config,
        CancellationToken cancellationToken = default);
}
