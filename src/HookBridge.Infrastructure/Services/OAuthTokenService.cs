using System.Collections.Concurrent;
using System.Text.Json;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;

namespace HookBridge.Infrastructure.Services;

public sealed class OAuthTokenService : IOAuthTokenService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public OAuthTokenService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAccessTokenAsync(OAuth2ClientCredentialsDto config, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{config.TokenUrl}|{config.ClientId}|{config.Scope}";
        if (_tokenCache.TryGetValue(cacheKey, out var cachedToken) && cachedToken.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return cachedToken.AccessToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_tokenCache.TryGetValue(cacheKey, out cachedToken) && cachedToken.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return cachedToken.AccessToken;
            }

            var client = _httpClientFactory.CreateClient();
            var form = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", config.ClientId),
                new("client_secret", config.ClientSecret),
            };

            if (!string.IsNullOrWhiteSpace(config.Scope))
            {
                form.Add(new("scope", config.Scope));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, config.TokenUrl)
            {
                Content = new FormUrlEncodedContent(form),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var tokenDoc = JsonDocument.Parse(json);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var expiresInElement)
                ? expiresInElement.GetInt32()
                : 3600;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("OAuth token response did not include access_token.");
            }

            var tokenLifetimeSeconds = Math.Max(1, expiresIn - 60);
            var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(tokenLifetimeSeconds);
            _tokenCache[cacheKey] = new CachedToken(accessToken, expiresAtUtc);
            return accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
