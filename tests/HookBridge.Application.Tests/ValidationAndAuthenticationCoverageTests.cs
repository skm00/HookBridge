using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation.TestHelper;
using HookBridge.Application.Configuration;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Models.Delivery;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.ApiKeys;
using HookBridge.Application.Validation.EndpointValidation;
using HookBridge.Application.Validation.Events;
using HookBridge.Application.Validation.Subscriptions;
using HookBridge.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Tests;

public sealed class ValidationAndAuthenticationCoverageTests
{
    [Fact]
    public void Validate_WhenWebhookEndpointIsHttps_ShouldNotHaveValidationError()
    {
        var validator = new EndpointValidationRequestDtoValidator(
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = true }),
            new TestHostEnvironment(Environments.Development));

        var result = validator.TestValidate(TestDataBuilders.EndpointValidationRequest());

        result.ShouldNotHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_WhenWebhookEndpointUsesUnsupportedScheme_ShouldHaveValidationError()
    {
        var validator = new EndpointValidationRequestDtoValidator(
            Options.Create(new SecuritySettings { AllowPrivateNetworkTargetUrls = true }),
            new TestHostEnvironment(Environments.Development));

        var result = validator.TestValidate(TestDataBuilders.EndpointValidationRequest("ftp://webhooks.example.com/orders"));

        result.ShouldHaveValidationErrorFor(x => x.TargetUrl);
    }

    [Fact]
    public void Validate_WhenApiNameIsPresent_ShouldNotHaveValidationError()
    {
        var validator = new CreateApiKeyRequestDtoValidator();

        var result = validator.TestValidate(TestDataBuilders.CreateApiKeyRequest("Production Webhooks"));

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenApiNameIsBlank_ShouldHaveValidationError()
    {
        var validator = new CreateApiKeyRequestDtoValidator();

        var result = validator.TestValidate(TestDataBuilders.CreateApiKeyRequest("   "));

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenRequestDtoHasEventTypeAndPayload_ShouldNotHaveValidationError()
    {
        var validator = new EventIngestionRequestDtoValidator();

        var result = validator.TestValidate(TestDataBuilders.WebhookEventRequest());

        result.ShouldNotHaveValidationErrorFor(x => x.EventType);
        result.ShouldNotHaveValidationErrorFor(x => x.Data);
    }

    [Fact]
    public void Validate_WhenRequestDtoHasMissingPayload_ShouldHaveValidationError()
    {
        var validator = new EventIngestionRequestDtoValidator();
        var request = TestDataBuilders.WebhookEventRequest();
        request.Data = null;

        var result = validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Data);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    public void CalculateDelay_WhenExponentialBackoff_ShouldDoubleDelayPerAttempt(int attemptNumber, int expectedSeconds)
    {
        var service = new RetryPolicyService();

        var delay = service.CalculateDelay(TestDataBuilders.RetryPolicy(initialDelaySeconds: 10), attemptNumber);

        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ShouldRetry_WhenAttemptIsAtMaxAttempts_ShouldReturnFalse()
    {
        var service = new RetryPolicyService();

        var shouldRetry = service.ShouldRetry(TestDataBuilders.RetryPolicy(maxAttempts: 3), attemptNumber: 3);

        shouldRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_WhenBasicAuthenticationConfigured_ShouldGenerateAuthorizationHeader()
    {
        var handler = new WebhookAuthenticationHandler(new StubOAuthTokenService("unused-token"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://webhooks.example.com/orders");
        var deliveryRequest = new WebhookDeliveryRequest
        {
            Authentication = new AuthenticationDto
            {
                Type = "Basic",
                Basic = new BasicAuthDto { Username = "client", Password = "secret" },
            },
        };

        await handler.ApplyAsync(request, deliveryRequest);

        request.Headers.Authorization!.Scheme.Should().Be("Basic");
        request.Headers.Authorization.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("client:secret")));
    }

    [Fact]
    public async Task ApplyAsync_WhenHmacAuthenticationConfigured_ShouldGenerateSignatureHeader()
    {
        var handler = new WebhookAuthenticationHandler(new StubOAuthTokenService("unused-token"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://webhooks.example.com/orders")
        {
            Content = new StringContent("{\"id\":123}", Encoding.UTF8, "application/json"),
        };
        var deliveryRequest = new WebhookDeliveryRequest
        {
            Authentication = new AuthenticationDto
            {
                Type = "HmacSignature",
                HmacSignature = new HmacSignatureDto { Secret = "top-secret", HeaderName = "x-signature", Algorithm = "HMACSHA256" },
            },
        };

        await handler.ApplyAsync(request, deliveryRequest);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("top-secret"));
        var expected = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes("{\"id\":123}"))).ToLowerInvariant();
        request.Headers.GetValues("x-signature").Single().Should().Be(expected);
    }

    [Fact]
    public async Task ApplyAsync_WhenOAuthConfigured_ShouldGenerateBearerTokenHeader()
    {
        var handler = new WebhookAuthenticationHandler(new StubOAuthTokenService("access-token-123"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://webhooks.example.com/orders");
        var deliveryRequest = new WebhookDeliveryRequest
        {
            Authentication = new AuthenticationDto
            {
                Type = "OAuth2ClientCredentials",
                OAuth2 = new OAuth2ClientCredentialsDto
                {
                    TokenUrl = "https://auth.example.com/token",
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    Scope = "webhooks:send",
                },
            },
        };

        await handler.ApplyAsync(request, deliveryRequest);

        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("access-token-123");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenEndpointReturnsAccessToken_ShouldReturnAndCacheToken()
    {
        var httpHandler = new RecordingHttpMessageHandler("{\"access_token\":\"oauth-token\",\"expires_in\":3600}");
        var service = new OAuthTokenService(new StubHttpClientFactory(new HttpClient(httpHandler)));
        var config = new OAuth2ClientCredentialsDto
        {
            TokenUrl = "https://auth.example.com/oauth/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scope = "events:write",
        };

        var firstToken = await service.GetAccessTokenAsync(config);
        var secondToken = await service.GetAccessTokenAsync(config);

        firstToken.Should().Be("oauth-token");
        secondToken.Should().Be("oauth-token");
        httpHandler.RequestBodies.Should().HaveCount(1);
        httpHandler.RequestBodies[0].Should().Contain("grant_type=client_credentials").And.Contain("client_id=client-id");
    }


    [Fact]
    public async Task GetAccessTokenAsync_WhenAccessTokenIsMissing_ShouldThrowInvalidOperationException()
    {
        var httpHandler = new RecordingHttpMessageHandler("{\"expires_in\":3600}");
        var service = new OAuthTokenService(new StubHttpClientFactory(new HttpClient(httpHandler)));

        var act = () => service.GetAccessTokenAsync(new OAuth2ClientCredentialsDto
        {
            TokenUrl = "https://auth.example.com/oauth/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("OAuth token response did not include access_token.");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenEndpointReturnsFailure_ShouldPropagateHttpRequestException()
    {
        var httpHandler = new RecordingHttpMessageHandler("{\"error\":\"invalid_client\"}", HttpStatusCode.Unauthorized);
        var service = new OAuthTokenService(new StubHttpClientFactory(new HttpClient(httpHandler)));

        var act = () => service.GetAccessTokenAsync(new OAuth2ClientCredentialsDto
        {
            TokenUrl = "https://auth.example.com/oauth/token",
            ClientId = "bad-client",
            ClientSecret = "bad-secret",
        });

        await act.Should().ThrowAsync<HttpRequestException>();
        httpHandler.RequestBodies.Should().ContainSingle(body => body.Contains("client_id=bad-client", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyAsync_WhenApiKeyHeaderAlreadyHasValue_ShouldPreserveExistingHeader()
    {
        var handler = new WebhookAuthenticationHandler(new StubOAuthTokenService("unused-token"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://webhooks.example.com/orders");
        request.Headers.TryAddWithoutValidation("x-api-key", "caller-provided");
        var deliveryRequest = new WebhookDeliveryRequest
        {
            Authentication = new AuthenticationDto
            {
                Type = "ApiKeyHeader",
                ApiKeyHeader = new ApiKeyHeaderDto { HeaderName = "x-api-key", HeaderValue = "configured-secret" },
            },
        };

        await handler.ApplyAsync(request, deliveryRequest);

        request.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be("caller-provided");
    }

    private sealed class StubOAuthTokenService(string token) : IOAuthTokenService
    {
        public Task<string> GetAccessTokenAsync(OAuth2ClientCredentialsDto config, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HookBridge.Application.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
