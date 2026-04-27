using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Models.Delivery;
using HookBridge.Infrastructure.Services;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class WebhookAuthenticationHandlerTests
{
    [Fact]
    public async Task ApplyAsync_AddsBasicAuthHeader()
    {
        var handler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com");
        request.Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json");
        var deliveryRequest = CreateDeliveryRequest("Basic");
        deliveryRequest.Authentication!.Basic = new BasicAuthDto { Username = "user", Password = "pass" };

        await handler.ApplyAsync(request, deliveryRequest);

        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal("dXNlcjpwYXNz", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ApplyAsync_AddsApiKeyHeader()
    {
        var handler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com");
        request.Headers.TryAddWithoutValidation("x-api-key", "");
        var deliveryRequest = CreateDeliveryRequest("ApiKeyHeader");
        deliveryRequest.Authentication!.ApiKeyHeader = new ApiKeyHeaderDto { HeaderName = "x-api-key", HeaderValue = "secret-key" };

        await handler.ApplyAsync(request, deliveryRequest);

        Assert.True(request.Headers.TryGetValues("x-api-key", out var values));
        Assert.Equal("secret-key", values!.Single());
    }

    [Fact]
    public async Task ApplyAsync_AddsHmacSignatureHeader()
    {
        var handler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com");
        request.Content = new StringContent("{\"orderId\":\"1001\"}", Encoding.UTF8, "application/json");
        var deliveryRequest = CreateDeliveryRequest("HmacSignature");
        deliveryRequest.Authentication!.HmacSignature = new HmacSignatureDto { Secret = "top-secret", HeaderName = "x-signature" };

        await handler.ApplyAsync(request, deliveryRequest);

        Assert.True(request.Headers.TryGetValues("x-signature", out var values));
        Assert.StartsWith("sha256=", values!.Single());
    }

    [Fact]
    public async Task ApplyAsync_UsesRawJsonBodyForHmacSignature()
    {
        var handler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        var jsonBody = "{\"orderId\":\"1001\"}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
        };
        var deliveryRequest = CreateDeliveryRequest("HmacSignature");
        deliveryRequest.Authentication!.HmacSignature = new HmacSignatureDto { Secret = "secret", HeaderName = string.Empty };

        await handler.ApplyAsync(request, deliveryRequest);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
        var expected = $"sha256={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonBody))).ToLowerInvariant()}";
        Assert.True(request.Headers.TryGetValues("x-hookbridge-signature", out var values));
        Assert.Equal(expected, values!.Single());
    }

    [Fact]
    public async Task ApplyAsync_AddsOAuthBearerToken()
    {
        var tokenService = new Mock<HookBridge.Application.Interfaces.IOAuthTokenService>();
        tokenService
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<OAuth2ClientCredentialsDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-123");

        var handler = new WebhookAuthenticationHandler(tokenService.Object);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com");
        var deliveryRequest = CreateDeliveryRequest("OAuth2ClientCredentials");
        deliveryRequest.Authentication!.OAuth2 = new OAuth2ClientCredentialsDto
        {
            TokenUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };

        await handler.ApplyAsync(request, deliveryRequest);

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "token-123"), request.Headers.Authorization);
    }

    [Fact]
    public async Task ApplyAsync_WithNone_DoesNotAddAuth()
    {
        var handler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com");
        var deliveryRequest = CreateDeliveryRequest("None");

        await handler.ApplyAsync(request, deliveryRequest);

        Assert.Null(request.Headers.Authorization);
        Assert.Empty(request.Headers);
    }

    [Fact]
    public async Task OAuthTokenService_CallsTokenEndpoint_AndCachesToken()
    {
        var tokenEndpointCalls = 0;
        var httpHandler = new DelegatingHandlerStub((request, _) =>
        {
            tokenEndpointCalls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://auth.example.com/token", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"cached-token\",\"expires_in\":3600}", Encoding.UTF8, "application/json"),
            });
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(httpHandler));
        var tokenService = new OAuthTokenService(factory.Object);
        var config = new OAuth2ClientCredentialsDto
        {
            TokenUrl = "https://auth.example.com/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scope = "scope.read",
        };

        var token1 = await tokenService.GetAccessTokenAsync(config);
        var token2 = await tokenService.GetAccessTokenAsync(config);

        Assert.Equal("cached-token", token1);
        Assert.Equal("cached-token", token2);
        Assert.Equal(1, tokenEndpointCalls);
    }

    [Fact]
    public async Task SendAsync_DoesNotLogSecrets()
    {
        var logger = new TestLogger<WebhookDeliveryClient>();
        var httpHandler = new DelegatingHandlerStub((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(httpHandler));
        var authHandler = new WebhookAuthenticationHandler(Mock.Of<HookBridge.Application.Interfaces.IOAuthTokenService>());
        var client = new WebhookDeliveryClient(factory.Object, authHandler, logger);

        var request = new WebhookDeliveryRequest
        {
            TargetUrl = "https://example.com",
            EventId = "evt-1",
            TenantId = "tenant-1",
            EventType = "order.created",
            Payload = new { orderId = "1001" },
            TimeoutSeconds = 5,
            Authentication = new AuthenticationDto
            {
                Type = "Basic",
                Basic = new BasicAuthDto
                {
                    Username = "user",
                    Password = "super-secret-password",
                },
            },
        };

        await client.SendAsync(request);

        var joined = string.Join('\n', logger.Records.Select(x => x.Message));
        Assert.DoesNotContain("super-secret-password", joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", joined, StringComparison.OrdinalIgnoreCase);
    }

    private static WebhookDeliveryRequest CreateDeliveryRequest(string authenticationType)
    {
        return new WebhookDeliveryRequest
        {
            TargetUrl = "https://example.com",
            EventId = "evt-1",
            TenantId = "tenant-1",
            EventType = "order.created",
            Payload = new { orderId = "1001" },
            TimeoutSeconds = 30,
            Authentication = new AuthenticationDto
            {
                Type = authenticationType,
            },
        };
    }

    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
