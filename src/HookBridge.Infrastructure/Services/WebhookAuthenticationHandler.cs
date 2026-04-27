using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Models.Delivery;

namespace HookBridge.Infrastructure.Services;

public sealed class WebhookAuthenticationHandler : IWebhookAuthenticationHandler
{
    private const string DefaultHmacHeaderName = "x-hookbridge-signature";
    private readonly IOAuthTokenService _oAuthTokenService;

    public WebhookAuthenticationHandler(IOAuthTokenService oAuthTokenService)
    {
        _oAuthTokenService = oAuthTokenService;
    }

    public async Task ApplyAsync(
        HttpRequestMessage httpRequest,
        WebhookDeliveryRequest deliveryRequest,
        CancellationToken cancellationToken = default)
    {
        var authentication = deliveryRequest.Authentication;
        var authenticationType = authentication?.Type ?? "None";

        switch (authenticationType)
        {
            case "None":
                return;
            case "Basic":
                ApplyBasicAuth(httpRequest, authentication?.Basic);
                return;
            case "ApiKeyHeader":
                ApplyApiKeyHeader(httpRequest, authentication?.ApiKeyHeader);
                return;
            case "HmacSignature":
                await ApplyHmacSignatureAsync(httpRequest, authentication?.HmacSignature, cancellationToken);
                return;
            case "OAuth2ClientCredentials":
                await ApplyOAuth2ClientCredentialsAsync(httpRequest, authentication?.OAuth2, cancellationToken);
                return;
            default:
                return;
        }
    }

    private static void ApplyBasicAuth(HttpRequestMessage httpRequest, BasicAuthDto? config)
    {
        if (config is null || string.IsNullOrWhiteSpace(config.Username))
        {
            return;
        }

        var credentials = $"{config.Username}:{config.Password}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    private static void ApplyApiKeyHeader(HttpRequestMessage httpRequest, ApiKeyHeaderDto? config)
    {
        if (config is null || string.IsNullOrWhiteSpace(config.HeaderName))
        {
            return;
        }

        if (httpRequest.Headers.TryGetValues(config.HeaderName, out var existingValues))
        {
            if (existingValues.Any(static value => !string.IsNullOrWhiteSpace(value)))
            {
                return;
            }

            httpRequest.Headers.Remove(config.HeaderName);
        }

        httpRequest.Headers.TryAddWithoutValidation(config.HeaderName, config.HeaderValue);
    }

    private static async Task ApplyHmacSignatureAsync(
        HttpRequestMessage httpRequest,
        HmacSignatureDto? config,
        CancellationToken cancellationToken)
    {
        if (config is null || string.IsNullOrWhiteSpace(config.Secret))
        {
            return;
        }

        var body = httpRequest.Content is null
            ? string.Empty
            : await httpRequest.Content.ReadAsStringAsync(cancellationToken);

        var keyBytes = Encoding.UTF8.GetBytes(config.Secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        using var hmac = new HMACSHA256(keyBytes);
        var signatureBytes = hmac.ComputeHash(bodyBytes);
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        var headerValue = $"sha256={signatureHex}";
        var headerName = string.IsNullOrWhiteSpace(config.HeaderName)
            ? DefaultHmacHeaderName
            : config.HeaderName;

        httpRequest.Headers.Remove(headerName);
        httpRequest.Headers.TryAddWithoutValidation(headerName, headerValue);
    }

    private async Task ApplyOAuth2ClientCredentialsAsync(
        HttpRequestMessage httpRequest,
        OAuth2ClientCredentialsDto? config,
        CancellationToken cancellationToken)
    {
        if (config is null)
        {
            return;
        }

        var accessToken = await _oAuthTokenService.GetAccessTokenAsync(config, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
