using System.Net;
using System.Text;
using FluentAssertions;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Infrastructure.Services;

namespace HookBridge.Application.Tests;

public sealed class OAuthTokenServiceNegativeTests
{
    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenEndpointReturnsBlankAccessToken_ShouldThrowInvalidOperationException()
    {
        var service = new OAuthTokenService(new StubHttpClientFactory(new HttpClient(new StaticHttpMessageHandler(
            HttpStatusCode.OK,
            "{\"access_token\":\"\",\"expires_in\":3600}"))));

        var act = () => service.GetAccessTokenAsync(CreateConfig());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenEndpointReturnsError_ShouldThrowHttpRequestException()
    {
        var service = new OAuthTokenService(new StubHttpClientFactory(new HttpClient(new StaticHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            "oauth unavailable"))));

        var act = () => service.GetAccessTokenAsync(CreateConfig());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static OAuth2ClientCredentialsDto CreateConfig() => new()
    {
        TokenUrl = "https://auth.example.com/token",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        Scope = "events:write",
    };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            });
        }
    }
}
